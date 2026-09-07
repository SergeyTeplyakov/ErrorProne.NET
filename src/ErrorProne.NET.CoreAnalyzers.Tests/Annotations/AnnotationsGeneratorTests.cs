using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorProne.NET.Annotations.Generation;
using ErrorProne.NET.AsyncAnalyzers;
using ErrorProne.NET.DisposableAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace ErrorProne.NET.CoreAnalyzers.Tests.Annotations;

[TestFixture]
public sealed class AnnotationsGeneratorTests
{
    private const int AnnotationCount = 9;

    private const string LibrarySource = @"
[assembly: Library.UseConfigureAwaitFalse]
namespace Library
{
    public sealed class Resource : System.IDisposable { public void Dispose() { } }
    public static class Api
    {
        [return: ReturnsOwnership] public static Resource Create() { return new Resource(); }
        [return: DoNotDispose] public static Resource GetShared() { return null; }
        public static void Take([AcquiresOwnership] Resource resource) { resource.Dispose(); }
        [MustUseResult] public static int Observe() { return 42; }
        [MustUseReturnValue] public static int ObserveAlias() { return 42; }
    }
}";

    private static async Task<CSharpCompilation> CreateCompilation(string name, string source,
        LanguageVersion languageVersion = LanguageVersion.CSharp7_3, MetadataReference? reference = null)
    {
        var references = await ReferenceAssemblies.Net.Net80.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        if (reference != null)
        {
            references = references.Add(reference);
        }

        return CSharpCompilation.Create(name,
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(languageVersion)) },
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static GeneratorDriver CreateDriver(CSharpCompilation compilation, string? rootNamespace = "Library",
        string? namespaceOverride = null)
    {
        return CSharpGeneratorDriver.Create(
            new[] { new AnnotationsGenerator().AsSourceGenerator() },
            parseOptions: (CSharpParseOptions)compilation.SyntaxTrees.Single().Options,
            optionsProvider: new OptionsProvider(rootNamespace, namespaceOverride));
    }

    private static CSharpCompilation Generate(CSharpCompilation compilation, int expectedCount,
        string rootNamespace = "Library", string? namespaceOverride = null)
    {
        var driver = CreateDriver(compilation, rootNamespace, namespaceOverride);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var diagnostics);
        Assert.That(diagnostics, Is.Empty);
        Assert.That(driver.GetRunResult().GeneratedTrees.Length, Is.EqualTo(expectedCount));
        Assert.That(updated.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty);
        return (CSharpCompilation)updated;
    }

    private static PortableExecutableReference Emit(CSharpCompilation compilation, bool referenceAssembly)
    {
        using var image = new MemoryStream();
        var result = compilation.Emit(image,
            options: new EmitOptions(metadataOnly: referenceAssembly, includePrivateMembers: !referenceAssembly));
        Assert.That(result.Success, Is.True, string.Join("\n", result.Diagnostics));
        return MetadataReference.CreateFromImage(image.ToArray());
    }

    [TestCase(LanguageVersion.CSharp7_3)]
    [TestCase(LanguageVersion.Latest)]
    public async Task Generates_Internal_Annotations_Without_Runtime_Dependencies(LanguageVersion languageVersion)
    {
        var compilation = Generate(await CreateCompilation("Library", LibrarySource, languageVersion), AnnotationCount);
        foreach (var name in new[] { "AcquiresOwnership", "ReturnsOwnership", "DoNotDispose",
            "KeepsOwnership", "NoOwnership", "MustUseResult", "MustUseReturnValue",
            "UseConfigureAwaitFalse", "DoNotUseConfigureAwait" })
        {
            var attribute = compilation.GetTypeByMetadataName("Library." + name + "Attribute");
            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute!.DeclaredAccessibility, Is.EqualTo(Accessibility.Internal));
            Assert.That(attribute.GetAttributes().Any(a => a.AttributeClass?.Name == "ConditionalAttribute"), Is.False);
        }

        var reference = Emit(compilation, referenceAssembly: false);
        var consumer = await CreateCompilation("Consumer", "", reference: reference);
        var library = (IAssemblySymbol)consumer.GetAssemblyOrModuleSymbol(reference)!;
        Assert.That(library.Modules.SelectMany(m => m.ReferencedAssemblySymbols)
            .Any(a => a.Name.StartsWith("ErrorProne")), Is.False);
    }

    [Test]
    public async Task Does_Not_Duplicate_An_Existing_Attribute()
    {
        var compilation = await CreateCompilation("Library", @"
namespace Library
{
    internal sealed class DoNotDisposeAttribute : System.Attribute { }
}");
        Generate(compilation, AnnotationCount - 1);
    }

    [TestCase("UseConfigureAwaitFalse")]
    [TestCase("DoNotUseConfigureAwait")]
    public async Task Reuses_An_Existing_Unsuffixed_ConfigureAwait_Attribute(string name)
    {
        var compilation = await CreateCompilation("Library", @"
[assembly: Library." + name + @"]
namespace Library
{
    [System.AttributeUsage(System.AttributeTargets.Assembly)]
    internal sealed class " + name + @" : System.Attribute { }
}");
        Generate(compilation, AnnotationCount - 1);
    }

    [Test]
    public async Task Does_Not_Treat_An_Unsuffixed_NonAttribute_Class_As_An_Annotation()
    {
        var compilation = await CreateCompilation("Library", @"
[assembly: Library.DoNotUseConfigureAwait]
namespace Library
{
    internal sealed class DoNotUseConfigureAwait { }
}");
        Generate(compilation, AnnotationCount);
    }

    [TestCase(null, AnnotationCount - 1, "ExistingAnnotations")]
    [TestCase("Existing", AnnotationCount, "Consumer")]
    [TestCase("global,Existing", AnnotationCount - 1, "ExistingAnnotations")]
    public async Task Reuses_Referenced_Attributes_Only_When_Globally_Accessible(
        string? aliases, int expectedCount, string expectedAssembly)
    {
        var library = await CreateCompilation("ExistingAnnotations", @"
namespace Library
{
    public sealed class DoNotDisposeAttribute : System.Attribute { }
}");
        var reference = Emit(library, referenceAssembly: false);
        if (aliases != null)
        {
            reference = reference.WithAliases(aliases.Split(','));
        }

        var consumer = await CreateCompilation("Consumer", @"
namespace Library
{
    public class Api
    {
        [return: DoNotDispose]
        public System.IDisposable GetShared() { return null; }
    }
}", reference: reference);
        var generated = Generate(consumer, expectedCount);
        var attribute = generated.GetTypeByMetadataName("Library.DoNotDisposeAttribute")!;
        Assert.That(attribute.ContainingAssembly.Name, Is.EqualTo(expectedAssembly));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Internal_Annotations_On_Public_APIs_Survive_Assembly_Boundaries(bool referenceAssembly)
    {
        var library = Generate(await CreateCompilation("Library", LibrarySource), AnnotationCount);
        var reference = Emit(library, referenceAssembly);
        var consumer = await CreateCompilation("Consumer", @"
public class UseLibrary
{
    public void Run()
    {
        var owned = Library.Api.Create();
        var borrowed = Library.Api.GetShared();
        borrowed.Dispose();
        var transferred = Library.Api.Create();
        Library.Api.Take(transferred);
        transferred.Dispose();
        Library.Api.Observe();
        Library.Api.ObserveAlias();
    }
}", reference: reference);
        Assert.That(consumer.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty);

        var annotations = consumer.GetTypeByMetadataName("Library.Api")!.GetMembers("Create")
            .OfType<IMethodSymbol>().Single().GetReturnTypeAttributes();
        Assert.That(annotations.Any(a => a.AttributeClass?.ToDisplayString()
            == "Library.ReturnsOwnershipAttribute"), Is.True);
        var librarySymbol = (IAssemblySymbol)consumer.GetAssemblyOrModuleSymbol(reference)!;
        Assert.That(librarySymbol.GetAttributes().Any(a => a.AttributeClass?.Name
            == "UseConfigureAwaitFalseAttribute"), Is.True);

        var diagnostics = await consumer.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(
            new DisposeBeforeLosingScopeAnalyzer(), new MustUseResultAnalyzer())).GetAnalyzerDiagnosticsAsync();
        Assert.That(diagnostics.Select(d => d.Id).OrderBy(id => id),
            Is.EqualTo(new[] { "EPC34", "EPC34", "ERP044", "ERP046", "ERP046" }));

        // The consumer can also annotate its own API without reusing inaccessible library types.
        var annotatedConsumer = await CreateCompilation("AnnotatedConsumer", @"
namespace Library
{
public class ConsumerApi
{
    [return: DoNotDispose]
    public System.IDisposable GetShared() { return Library.Api.GetShared(); }
}
}", reference: reference);
        Generate(annotatedConsumer, AnnotationCount);
    }

    [TestCase("MustUseResult")]
    [TestCase("MustUseReturnValue")]
    public async Task Generated_Result_Annotations_Require_Observing_Sync_And_Async_Results(string attribute)
    {
        var compilation = Generate(await CreateCompilation("Library", @"
using System.Threading.Tasks;
namespace Library
{
    public static class Api
    {
        [" + attribute + @"] public static int Observe() { return 42; }
        [" + attribute + @"] public static Task<int> ObserveAsync() { return Task.FromResult(42); }
        public static async Task Run()
        {
            Observe();
            _ = Observe();
            await ObserveAsync();
            _ = await ObserveAsync();
        }
    }
}"), AnnotationCount);

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(
            new MustUseResultAnalyzer())).GetAnalyzerDiagnosticsAsync();
        Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "EPC34", "EPC34" }));
    }

    [TestCase(null, null)]
    [TestCase("UseConfigureAwaitFalse", "EPC15")]
    [TestCase("DoNotUseConfigureAwait", "EPC14")]
    public async Task Generated_Assembly_Annotations_Configure_Await_Policy(string? attribute, string? expectedDiagnostic)
    {
        var assemblyAttribute = attribute == null ? "" : "[assembly: Library." + attribute + "]\n";
        var compilation = Generate(await CreateCompilation("Library", "using System.Threading.Tasks;\n" + assemblyAttribute + @"
namespace Library
{
    public static class Api
    {
        public static async Task Run()
        {
            await Task.Delay(1);
            await Task.Delay(1).ConfigureAwait(false);
        }
    }
}"), AnnotationCount);

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(
            new ConfigureAwaitRequiredAnalyzer(), new RedundantConfigureAwaitFalseAnalyzer())).GetAnalyzerDiagnosticsAsync();
        Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(expectedDiagnostic == null
            ? System.Array.Empty<string>() : new[] { expectedDiagnostic }));
    }

    [TestCase("MustUseResult", System.AttributeTargets.Method)]
    [TestCase("MustUseReturnValue", System.AttributeTargets.Method)]
    [TestCase("UseConfigureAwaitFalse", System.AttributeTargets.Assembly)]
    [TestCase("DoNotUseConfigureAwait", System.AttributeTargets.Assembly)]
    public async Task Generated_Non_Ownership_Annotations_Have_Expected_Targets(
        string name, System.AttributeTargets expectedTargets)
    {
        var compilation = Generate(await CreateCompilation("Library", ""), AnnotationCount);
        var attribute = compilation.GetTypeByMetadataName("Library." + name + "Attribute")!;
        var usage = attribute.GetAttributes().Single(a => a.AttributeClass?.Name == "AttributeUsageAttribute");
        Assert.That(usage.ConstructorArguments.Single().Value, Is.EqualTo((int)expectedTargets));
    }

    [TestCase("Azure.Core", null, "Azure.Core")]
    [TestCase("Azure.Core", "Azure.Core.InternalAnnotations", "Azure.Core.InternalAnnotations")]
    [TestCase("Azure.Core", "", "Azure.Core")]
    [TestCase("", null, "")]
    [TestCase("class.namespace", null, "class.namespace")]
    public async Task Uses_Configured_Namespace(string rootNamespace, string? namespaceOverride, string expected)
    {
        var compilation = Generate(await CreateCompilation("DifferentAssemblyName", ""), AnnotationCount, rootNamespace, namespaceOverride);
        var prefix = expected.Length == 0 ? "" : expected + ".";
        Assert.That(compilation.GetTypeByMetadataName(prefix + "DoNotDisposeAttribute"), Is.Not.Null);
    }

    [TestCase(null, "EPANN001")]
    [TestCase("Azure..Core", "EPANN002")]
    [TestCase("Azure-Core", "EPANN002")]
    [TestCase("global::Azure.Core", "EPANN002")]
    [TestCase("Azure.Core<T>", "EPANN002")]
    public async Task Reports_Missing_Or_Invalid_Namespace(string? rootNamespace, string expectedDiagnostic)
    {
        var compilation = await CreateCompilation("Library", "");
        var driver = CreateDriver(compilation, rootNamespace).RunGenerators(compilation);
        Assert.That(driver.GetRunResult().Diagnostics.Select(d => d.Id), Is.EqualTo(new[] { expectedDiagnostic }));
        Assert.That(driver.GetRunResult().GeneratedTrees, Is.Empty);
    }

    private static async Task<PortableExecutableReference> CreateFriendLibrary(string name)
    {
        var compilation = await CreateCompilation(name, @"
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""Consumer"")]
namespace Azure.Core
{
    internal sealed class DoNotDisposeAttribute : System.Attribute { }
}");
        return Emit(compilation, referenceAssembly: false);
    }

    [Test]
    public async Task Friend_Assembly_Can_Omit_The_Generator()
    {
        var compilation = await CreateCompilation("Consumer", @"
namespace Azure.Core
{
    public class Api
    {
        [return: DoNotDispose]
        public System.IDisposable GetShared() { return null; }
    }
}", reference: await CreateFriendLibrary("Library"));
        Assert.That(compilation.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty);
    }

    [Test]
    public async Task Reuses_Accessible_Friend_Attribute_Instead_Of_Duplicating_It()
    {
        var compilation = await CreateCompilation("Consumer", "", reference: await CreateFriendLibrary("Library"));
        var generated = Generate(compilation, AnnotationCount - 1, rootNamespace: "Azure.Core");
        Assert.That(generated.GetTypeByMetadataName("Azure.Core.DoNotDisposeAttribute")!
            .ContainingAssembly.Name, Is.EqualTo("Library"));
    }

    [Test]
    public async Task Ambiguous_Friend_Attributes_Require_An_Explicit_Namespace_Override()
    {
        var compilation = (await CreateCompilation("Consumer", ""))
            .AddReferences(await CreateFriendLibrary("FirstLibrary"), await CreateFriendLibrary("SecondLibrary"));
        var driver = CreateDriver(compilation, "Azure.Core").RunGenerators(compilation);
        Assert.That(driver.GetRunResult().Diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "EPANN003" }));
        Assert.That(driver.GetRunResult().GeneratedTrees.Length, Is.EqualTo(AnnotationCount - 1));

        var generated = Generate(compilation, AnnotationCount, "Azure.Core", "Azure.Core.InternalAnnotations");
        Assert.That(generated.GetTypeByMetadataName("Azure.Core.InternalAnnotations.DoNotDisposeAttribute")!
            .ContainingAssembly.Name, Is.EqualTo("Consumer"));
    }

    [Test]
    public async Task Local_Definition_Takes_Precedence_Over_Ambiguous_Friend_Attributes()
    {
        var compilation = (await CreateCompilation("Consumer", @"
namespace Azure.Core
{
    internal sealed class DoNotDisposeAttribute : System.Attribute { }
}")).AddReferences(await CreateFriendLibrary("FirstLibrary"), await CreateFriendLibrary("SecondLibrary"));
        var generated = Generate(compilation, AnnotationCount - 1, rootNamespace: "Azure.Core");
        Assert.That(generated.GetTypeByMetadataName("Azure.Core.DoNotDisposeAttribute")!
            .ContainingAssembly.Name, Is.EqualTo("Consumer"));
    }

    private sealed class OptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly Options _options;
        public OptionsProvider(string? rootNamespace, string? namespaceOverride)
        {
            var values = new Dictionary<string, string>();
            if (rootNamespace != null) values["build_property.RootNamespace"] = rootNamespace;
            if (namespaceOverride != null) values["build_property.ErrorProneAnnotationsNamespace"] = namespaceOverride;
            _options = new Options(values);
        }
        public override AnalyzerConfigOptions GlobalOptions => _options;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
    }

    private sealed class Options : AnalyzerConfigOptions
    {
        private readonly IReadOnlyDictionary<string, string> _values;
        public Options(IReadOnlyDictionary<string, string> values) { _values = values; }
        public override bool TryGetValue(string key, out string value)
        {
            if (_values.TryGetValue(key, out var found))
            {
                value = found;
                return true;
            }
            value = "";
            return false;
        }
    }
}

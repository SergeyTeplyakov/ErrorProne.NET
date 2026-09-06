using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ErrorProne.NET.TestHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.DisposableAnalyzers.DisposeBeforeLoosingScopeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.DisposableAnalyzers;

[TestFixture]
public sealed class OwnershipContractsTests
{
    private const string Prelude = @"
[System.AttributeUsage(System.AttributeTargets.All)]
public sealed class AcquiresOwnershipAttribute : System.Attribute { }
[System.AttributeUsage(System.AttributeTargets.All)]
public sealed class NoOwnershipAttribute : System.Attribute { }
[System.AttributeUsage(System.AttributeTargets.All)]
public sealed class ReturnsOwnershipAttribute : System.Attribute { }
[System.AttributeUsage(System.AttributeTargets.All)]
public sealed class KeepsOwnershipAttribute : System.Attribute { }
[System.AttributeUsage(System.AttributeTargets.All)]
public sealed class DoNotDisposeAttribute : System.Attribute { }
public sealed class Resource : System.IDisposable
{
    public void Dispose() { }
}
";

    private static Verify.Test CreateTest(string source, string xml)
    {
        var test = new Verify.Test
        {
            LanguageVersion = LanguageVersion.Latest,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source + Prelude,
        }.WithoutGeneratedCodeVerification();
        test.TestState.AdditionalFiles.Add(("Contracts.ownership.xml", xml));
        return test;
    }

    [Test]
    public Task ExternalBorrowedReturnsRemainBorrowedThroughLocalAliases()
    {
        return CreateTest(@"
class Api
{
    public static Resource Read() => null;
    public static void Run()
    {
        var resource = Read();
        var alias = resource;
        {|ERP046:alias|}.Dispose();
    }
}", @"<ownership><member id=""M:Api.Read"" returns=""borrowed"" /></ownership>").RunAsync();
    }

    [Test]
    public Task DoNotDisposeOverridesAnExternalOwningReturn()
    {
        return CreateTest(@"
class Api
{
    [return: DoNotDispose] public static Resource Read() => null;
    public static void Run() { var resource = Read(); {|ERP046:resource|}.Dispose(); }
}", @"<ownership><member id=""M:Api.Read"" returns=""owned"" /></ownership>").RunAsync();
    }

    [Test]
    public Task DoNotDisposeParameterOverridesSourceDisposalInference()
    {
        return CreateTest(@"
class Api
{
    public static void Inspect([DoNotDispose] Resource resource) { {|ERP046:resource|}.Dispose(); }
    public static void Run()
    {
        var resource = new Resource();
        Inspect(resource);
        resource.Dispose();
    }
}", "<ownership />").RunAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public Task ExternalBorrowedReturnsDoNotSuppressAcquiredParameters(bool dispose)
    {
        return CreateTest(@"
class Api
{
    public static Resource BorrowOther(
        Resource " + (dispose ? "owned" : "{|ERP044:owned|}") + @", Resource borrowed)
    {
        " + (dispose ? "owned.Dispose();" : "") + @"
        return borrowed;
    }
    public static void Run(Resource borrowed)
    {
        var result = BorrowOther(new Resource(), borrowed);
    }
}", @"<ownership>
  <member id=""M:Api.BorrowOther(Resource,Resource)"" returns=""borrowed"">
    <parameter name=""owned"" ownership=""owned"" />
    <parameter name=""borrowed"" ownership=""borrowed"" />
  </member>
</ownership>").RunAsync();
    }

    [Test]
    public Task ExternalReturnOwnershipIsHonored()
    {
        return CreateTest(@"
class Api
{
    public static Resource Read() => null;
    public static Resource Create() => null;
    public static void Run()
    {
        var {|ERP044:owned|} = Read();
        var borrowed = Create();
    }
}", @"<ownership>
  <member id=""M:Api.Read"" returns=""owned"" />
  <member id=""M:Api.Create"" returns=""borrowed"" />
</ownership>").RunAsync();
    }

    [Test]
    public Task ExternalPropertyOwnershipIsHonored()
    {
        return CreateTest(@"
class Api
{
    public static Resource Owned => null;
    public static Resource Borrowed => null;
    public static void Run()
    {
        var {|ERP044:owned|} = Owned;
        var borrowed = Borrowed;
    }
}", @"<ownership>
  <member id=""P:Api.Owned"" returns=""owned"" />
  <member id=""P:Api.Borrowed"" returns=""borrowed"" />
</ownership>").RunAsync();
    }

    [Test]
    public Task ExternalBorrowingOverridesDirectFreshReturnInference()
    {
        return CreateTest(@"
class Api
{
    public static Resource Create() => new Resource();
    public static void Run() { var resource = Create(); {|ERP046:resource|}.Dispose(); }
}", @"<ownership><member id=""M:Api.Create"" returns=""borrowed"" /></ownership>").RunAsync();
    }

    [TestCase("Task", "")]
    [TestCase("Task", ".ConfigureAwait(false)")]
    [TestCase("ValueTask", "")]
    [TestCase("ValueTask", ".ConfigureAwait(false)")]
    public Task ExternalAwaitedPropertyContractsAreHonored(string taskType, string configure)
    {
        return CreateTest(@"
using System.Threading.Tasks;
class Api
{
    public static " + taskType + @"<Resource> Owned => default;
    public static " + taskType + @"<Resource> Borrowed => default;
    public static async Task Run()
    {
        var {|ERP044:owned|} = await Owned" + configure + @";
        using var borrowed = {|ERP046:await Borrowed" + configure + @"|};
        using var cleaned = await Owned" + configure + @";
    }
}", @"<ownership>
  <member id=""P:Api.Owned"" returns=""owned"" />
  <member id=""P:Api.Borrowed"" returns=""borrowed"" />
</ownership>").RunAsync();
    }

    [Test]
    public Task ExternalInterlockedParameterContractDoesNotDuplicateBorrowedDiagnostics()
    {
        return CreateTest(@"
class Api
{
    private Resource _owned;
    public void Run([DoNotDispose] Resource borrowed)
    {
        System.Threading.Interlocked.Exchange(ref _owned, {|ERP046:borrowed|});
    }
}", @"<ownership>
  <member id=""M:System.Threading.Interlocked.Exchange``1(``0@,``0)"">
    <parameter name=""value"" ownership=""owned"" />
  </member>
</ownership>").RunAsync();
    }

    [Test]
    public Task ExternalIndexerParameterContractsAreHonored()
    {
        return CreateTest(@"
class Api
{
    public int this[Resource value, Resource other]
    {
        get { value?.Dispose(); return 0; }
    }
    public void Run([DoNotDispose] Resource borrowed)
    {
        _ = this[other: null, value: new Resource()];
        _ = this[other: null, value: {|ERP046:borrowed|}];
        var retained = new Resource();
        _ = this[other: retained, value: null];
        retained.Dispose();
        Forward(new Resource());
    }
    private void Forward(Resource d) { _ = this[other: null, value: d]; }
}", @"<ownership>
  <member id=""P:Api.Item(Resource,Resource)"">
    <parameter name=""value"" ownership=""owned"" />
  </member>
</ownership>").RunAsync();
    }

    [TestCase("Task", "owned")]
    [TestCase("Task", "borrowed")]
    [TestCase("ValueTask", "owned")]
    [TestCase("ValueTask", "borrowed")]
    public Task ExplicitConfigureAwaitContractsOverrideUnderlyingPropertyContracts(string taskType, string ownership)
    {
        var body = ownership == "owned"
            ? "var {|ERP044:resource|} = await Pending.ConfigureAwait(false);"
            : "using var resource = {|ERP046:await Pending.ConfigureAwait(false)|};";
        var attribute = ownership == "owned" ? "DoNotDispose" : "ReturnsOwnership";
        return CreateTest(@"
using System.Threading.Tasks;
class Api
{
    [" + attribute + @"] private " + taskType + @"<Resource> Pending => default;
    public async Task Run() { " + body + @" }
}", @"<ownership>
  <member id=""M:System.Threading.Tasks." + taskType + @"`1.ConfigureAwait(System.Boolean)"" returns=""" + ownership + @""" />
</ownership>").RunAsync();
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task ExternalLibraryBoundariesNeedExplicitContracts(bool annotated, bool compiled)
    {
        var references = await ReferenceAssemblies.Net.Net80.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var library = CSharpCompilation.Create("Library",
            new[] { CSharpSyntaxTree.ParseText(@"
namespace Library
{
    public class Resource : System.IDisposable { public void Dispose() { } }
    public static class Factory
    {
        private static readonly Resource shared = new Resource();
        public static Resource Create() => new Resource();
        public static Resource GetShared() => shared;
    }
    public static class Resources { public static Resource Shared { get; } = new Resource(); }
    public static class Consumer { public static void Take(System.IDisposable resource) { resource.Dispose(); } }
}") },
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var test = CreateTest(@"
class Api
{
    public static void Run()
    {
        var " + (annotated ? "{|ERP044:resource|}" : "resource") + @" = Library.Factory.Create();
        var borrowed = Library.Factory.GetShared();
        var alias = borrowed;
        " + (annotated ? "{|ERP046:alias|}" : "alias") + @".Dispose();
        using var property = " + (annotated ? "{|ERP046:Library.Resources.Shared|}" : "Library.Resources.Shared") + @";
        Library.Consumer.Take(" + (annotated ? "{|ERP046:Library.Factory.GetShared()|}" : "Library.Factory.GetShared()") + @");
        var transferred = Library.Factory.Create();
        Library.Consumer.Take(transferred);
        " + (annotated ? "{|ERP046:transferred|}" : "transferred") + @".Dispose();
        Library.Consumer.Take(" + (annotated ? "new Library.Resource()" : "{|ERP044:new Library.Resource()|}") + @");
    }
}", annotated
            ? @"<ownership>
  <member id=""M:Library.Factory.Create"" assembly=""Library"" returns=""owned"" />
  <member id=""M:Library.Factory.GetShared"" assembly=""Library"" returns=""borrowed"" />
  <member id=""P:Library.Resources.Shared"" assembly=""Library"" returns=""borrowed"" />
  <member id=""M:Library.Consumer.Take(System.IDisposable)"" assembly=""Library"">
    <parameter name=""resource"" ownership=""owned"" />
  </member>
</ownership>"
            : "<ownership />");
        if (compiled)
        {
            using var image = new MemoryStream();
            var result = library.Emit(image);
            Assert.That(result.Success, Is.True, string.Join("\n", result.Diagnostics));
            test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromImage(image.ToArray()));
        }
        else
        {
            test.TestState.AdditionalReferences.Add(library.ToMetadataReference());
        }
        await test.RunAsync();
    }

    [Test]
    public Task ExternalMoveIsTrustedByCallerAndCheckedAtCallee()
    {
        return CreateTest(@"
class Api
{
    public static void Take(Resource {|ERP044:resource|}) { }
    public static void Run()
    {
        var moved = new Resource();
        Take(moved);
    }
}", @"<ownership>
  <member id=""M:Api.Take(Resource)"">
    <parameter name=""resource"" ownership=""owned"" />
  </member>
</ownership>").RunAsync();
    }

    [Test]
    public Task ParameterNamesAndOverloadsUseBoundArguments()
    {
        return CreateTest(@"
class Api
{
    public static void Take(int count, Resource resource) { resource.Dispose(); }
    public static void Take(Resource resource) { }
    public static void Run()
    {
        var moved = new Resource();
        Take(resource: moved, count: 1);
        var {|ERP044:retained|} = new Resource();
        Take(retained);
    }
}", @"<ownership>
  <member id=""M:Api.Take(System.Int32,Resource)"">
    <parameter name=""resource"" ownership=""owned"" />
  </member>
</ownership>").RunAsync();
    }

    [Test]
    public Task BorrowedParameterDoesNotTransferCallerOwnership()
    {
        return CreateTest(@"
class Api
{
    public static void Take(Resource resource) { }
    public static void Run()
    {
        var {|ERP044:retained|} = new Resource();
        Take(retained);
    }
}", @"<ownership>
  <member id=""M:Api.Take(Resource)"">
    <parameter name=""resource"" ownership=""borrowed"" />
  </member>
</ownership>").RunAsync();
    }

    [Test]
    public Task SourceReturnAttributesOverrideExternalContracts()
    {
        return CreateTest(@"
class Api
{
    [ReturnsOwnership] public static Resource Read() => null;
    [KeepsOwnership] public static Resource Create() => null;
    public static void Run()
    {
        var {|ERP044:owned|} = Read();
        var borrowed = Create();
    }
}", @"<ownership>
  <member id=""M:Api.Read"" returns=""borrowed"" />
  <member id=""M:Api.Create"" returns=""owned"" />
</ownership>").RunAsync();
    }

    [Test]
    public Task SourceParameterAttributesOverrideExternalContractsAndBorrowingWins()
    {
        return CreateTest(@"
class Api
{
    public static void Take([AcquiresOwnership] Resource resource) { resource.Dispose(); }
    public static void Borrow([NoOwnership, AcquiresOwnership] Resource resource) { }
    public static void Run()
    {
        var moved = new Resource();
        Take(moved);
        var {|ERP044:retained|} = new Resource();
        Borrow(retained);
    }
}", @"<ownership>
  <member id=""M:Api.Take(Resource)"">
    <parameter name=""resource"" ownership=""borrowed"" />
  </member>
  <member id=""M:Api.Borrow(Resource)"">
    <parameter name=""resource"" ownership=""owned"" />
  </member>
</ownership>").RunAsync();
    }

    [Test]
    public Task ReturnTargetAttributesAreHonored()
    {
        return CreateTest(@"
class Api
{
    [return: ReturnsOwnership] public static Resource Read() => null;
    [return: KeepsOwnership] public static Resource Create() => null;
    public static Resource Property { [return: ReturnsOwnership] get => null; }
    public static void Run()
    {
        var {|ERP044:owned|} = Read();
        var borrowed = Create();
        var {|ERP044:property|} = Property;
    }
}", "<ownership />").RunAsync();
    }

    [Test]
    public Task InterfaceReturnContractAppliesToConcreteGenericImplementation()
    {
        return CreateTest(@"
interface IFactory<T>
{
    [return: KeepsOwnership] T Read();
    [ReturnsOwnership] T Value { get; }
}
class Factory : IFactory<Resource>
{
    public Resource Read() => null;
    public Resource Value => null;
    public static void Run()
    {
        var factory = new Factory();
        var borrowed = factory.Read();
        var {|ERP044:owned|} = factory.Value;
    }
}", "<ownership />").RunAsync();
    }

    [Test]
    public Task InterfaceParameterOwnershipAlsoAppliesToRenamedImplementationParameter()
    {
        return CreateTest(@"
interface IConsumer
{
    void Take([AcquiresOwnership] Resource resource);
}
class Consumer : IConsumer
{
    public void Take(Resource {|ERP044:renamed|}) { }
    public static void Run()
    {
        var moved = new Resource();
        new Consumer().Take(moved);
    }
}", "<ownership />").RunAsync();
    }

    [Test]
    public Task ExternalInterfaceContractAlsoAppliesToImplementation()
    {
        return CreateTest(@"
interface IConsumer
{
    void Take(Resource resource);
}
class Consumer : IConsumer
{
    public void Take(Resource {|ERP044:renamed|}) { }
    public static void Run()
    {
        var moved = new Resource();
        new Consumer().Take(moved);
    }
}", @"<ownership>
  <member id=""M:IConsumer.Take(Resource)"">
    <parameter name=""resource"" ownership=""owned"" />
  </member>
</ownership>").RunAsync();
    }

    [Test]
    public Task OverriddenParameterOwnershipUsesOrdinalRatherThanName()
    {
        return CreateTest(@"
abstract class Base
{
    public abstract void Take([AcquiresOwnership] Resource resource);
}
class Derived : Base
{
    public override void Take(Resource {|ERP044:renamed|}) { }
    public static void Run()
    {
        var moved = new Resource();
        new Derived().Take(moved);
    }
}", "<ownership />").RunAsync();
    }

    [Test]
    public Task InheritedSourceContractOverridesDirectExternalContract()
    {
        return CreateTest(@"
interface IConsumer
{
    void Take([NoOwnership] Resource resource);
}
class Consumer : IConsumer
{
    public void Take(Resource renamed) { }
    public static void Run()
    {
        var {|ERP044:retained|} = new Resource();
        new Consumer().Take(retained);
    }
}", @"<ownership>
  <member id=""M:Consumer.Take(Resource)"">
    <parameter name=""renamed"" ownership=""owned"" />
  </member>
</ownership>").RunAsync();
    }

    [Test]
    public Task ConstructedGenericReturnAndParameterContractsUseDefinitions()
    {
        return CreateTest(@"
class Api<T> where T : System.IDisposable
{
    public static T Read() => default(T);
    public static T Value => default(T);
    public static void Take(T {|ERP044:resource|}) { }
}
class Caller
{
    public static void Run()
    {
        var borrowed = Api<Resource>.Read();
        var {|ERP044:owned|} = Api<Resource>.Value;
        var moved = new Resource();
        Api<Resource>.Take(moved);
    }
}", @"<ownership>
  <member id=""M:Api`1.Read"" returns=""borrowed"" />
  <member id=""P:Api`1.Value"" returns=""owned"" />
  <member id=""M:Api`1.Take(`0)"">
    <parameter name=""resource"" ownership=""owned"" />
  </member>
</ownership>").RunAsync();
    }

    [Test]
    public Task ReducedGenericExtensionUsesOriginalParameterOrdinal()
    {
        return CreateTest(@"
static class Extensions
{
    public static void Take<T>(this string label, T {|ERP044:resource|}) where T : System.IDisposable { }
    public static T Read<T>(this string label) where T : System.IDisposable => default(T);
    public static void Consume<T>([AcquiresOwnership] this T resource) where T : System.IDisposable
    {
        resource.Dispose();
    }
}
class Caller
{
    public static void Run()
    {
        var moved = new Resource();
        ""label"".Take(moved);
        var borrowed = ""label"".Read<Resource>();
        var receiver = new Resource();
        receiver.Consume();
    }
}", @"<ownership>
  <member id=""M:Extensions.Take``1(System.String,``0)"">
    <parameter name=""resource"" ownership=""owned"" />
  </member>
  <member id=""M:Extensions.Read``1(System.String)"" returns=""borrowed"" />
</ownership>").RunAsync();
    }

    [Test]
    public Task AssemblyQualifierMatchesCurrentAssembly()
    {
        var test = CreateTest(@"
class Api
{
    public static Resource Read() => null;
    public static void Run() { var {|ERP044:owned|} = Read(); }
}", @"<ownership>
  <member id=""M:Api.Read"" assembly=""ContractTarget"" returns=""owned"" />
</ownership>");
        test.SolutionTransforms.Add((solution, project) => solution.WithProjectAssemblyName(project, "ContractTarget"));
        return test.RunAsync();
    }

    [Test]
    public Task AssemblyQualifierDoesNotMatchDifferentAssembly()
    {
        return CreateTest(@"
class Api
{
    public static Resource Value => null;
    public static void Run() { var borrowed = Value; }
}", @"<ownership>
  <member id=""P:Api.Value"" assembly=""SomeOtherLibrary"" returns=""owned"" />
</ownership>").RunAsync();
    }

    [Test]
    public Task MetadataMethodCanBeAnnotatedByAssemblyName()
    {
        return CreateTest(@"
class Caller
{
    public static void Run()
    {
        var borrowed = System.IO.File.OpenRead(""unused"");
    }
}", @"<ownership>
  <member id=""M:System.IO.File.OpenRead(System.String)"" assembly=""System.Runtime"" returns=""borrowed"" />
</ownership>").RunAsync();
    }

    [Test]
    public Task AbsentLibrariesAndValidComplexIdentifiersAreAllowed()
    {
        return CreateTest("class Caller { }", @"<ownership>
  <member id=""M:Absent.Api`1.Read``1(`0,``0,System.Collections.Generic.List{System.String},System.Int32[0:,0:],System.Byte*,System.String@)""
          assembly=""AbsentLibrary"" returns=""owned"" />
  <member id=""M:Absent.Api.#ctor(System.IDisposable)"">
    <parameter name=""resource"" ownership=""owned"" />
  </member>
  <member id=""M:Absent.Api.op_Implicit(Absent.Api)~System.IDisposable"" returns=""owned"" />
  <member id=""P:Absent.Api.Item(System.Int32)"" returns=""borrowed"" />
</ownership>").RunAsync();
    }

    [Test]
    public Task UnrelatedAdditionalFilesAreIgnored()
    {
        var test = CreateTest("class Caller { }", "<ownership />");
        test.TestState.AdditionalFiles.Add(("unrelated.xml", "<not valid xml"));
        return test.RunAsync();
    }

    [Test]
    public Task DuplicateMembersAcrossFilesAreRejected()
    {
        var test = CreateTest("class Caller { }",
            @"<ownership><member id=""M:Absent.Api.Read"" returns=""owned"" /></ownership>");
        test.TestState.AdditionalFiles.Add(("Other.ownership.xml",
            @"<ownership><member id=""M:Absent.Api.Read"" returns=""owned"" /></ownership>"));
        test.ExpectedDiagnostics.Add(Verify.Diagnostic("ERP045")
            .WithMessage(null)
            .WithLocation("Other.ownership.xml", 1, 13));
        return test.RunAsync();
    }

    [TestCase("<^broken />")]
    [TestCase("<ownership><^unknown /></ownership>")]
    [TestCase("<ownership><^member id=\"M:Api.Read\" returns=\"shared\" /></ownership>")]
    [TestCase("<ownership><^member id=\"not-a-documentation-id\" returns=\"owned\" /></ownership>")]
    [TestCase("<ownership><^member id=\"M:Api.Read(System.String\" returns=\"owned\" /></ownership>")]
    [TestCase("<ownership><^member id=\"M:Api.Read(System.String,)\" returns=\"owned\" /></ownership>")]
    [TestCase("<ownership><member id=\"M:Api.Read\" ^typo=\"owned\" /></ownership>")]
    [TestCase("<ownership><member id=\"M:Api.Take(Resource)\"><^parameter name=\"resource\" ownership=\"shared\" /></member></ownership>")]
    [TestCase("<ownership><^member id=\"M:Api.Take(Resource)\"><parameter name=\"missing\" ownership=\"owned\" /></member></ownership>")]
    [TestCase("<ownership><member id=\"M:Api.Take(Resource)\"><parameter name=\"resource\" ownership=\"owned\" /><^parameter name=\"resource\" ownership=\"borrowed\" /></member></ownership>")]
    [TestCase("<ownership><member id=\"M:Api.Read\" returns=\"owned\" /><^member id=\"M:Api.Read\" returns=\"borrowed\" /></ownership>")]
    [TestCase("<ownership><member id=\"M:Api.Read\" returns=\"owned\" /><^member id=\"M:Api.Read\" assembly=\"ApiLibrary\" returns=\"borrowed\" /></ownership>")]
    [TestCase("<ownership><^member id=\"M:Api.Read\" /></ownership>")]
    [TestCase("<ownership><^member id=\"M:Api.Read\" assembly=\"\" returns=\"owned\" /></ownership>")]
    [TestCase("<^ownership>unexpected text</ownership>")]
    [TestCase("<ownership>^")]
    [TestCase("^<!DOCTYPE ownership [<!ENTITY dangerous SYSTEM \"file:///nonexistent\">]><ownership>&dangerous;</ownership>")]
    public Task InvalidAnnotationsReportAtAdditionalFile(string xml)
    {
        var column = xml.IndexOf('^') + 1;
        var test = CreateTest(@"
class Api
{
    public static Resource Read() => null;
    public static void Take(Resource resource) { resource.Dispose(); }
}", xml.Remove(column - 1, 1));
        test.ExpectedDiagnostics.Add(Verify.Diagnostic("ERP045")
            .WithMessage(null)
            .WithLocation("Contracts.ownership.xml", 1, column));
        return test.RunAsync();
    }
}

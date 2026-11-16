using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace ErrorProne.NET.CoreAnalyzers.Tests;

[TestFixture]
public sealed class TypeExtensionsTests
{
    [TestCase("BaseInt", "BaseDefinition", true)]
    [TestCase("Derived", "BaseDefinition", true)]
    [TestCase("Derived", "BaseInt", true)]
    [TestCase("Derived", "BaseString", false)]
    [TestCase("BaseInt", "BaseString", false)]
    [TestCase("BaseDefinition", "BaseInt", false)]
    [TestCase("InterfaceInt", "InterfaceDefinition", true)]
    [TestCase("Derived", "InterfaceDefinition", true)]
    [TestCase("Derived", "InterfaceInt", true)]
    [TestCase("Derived", "InterfaceString", false)]
    [TestCase("Derived", "InterfaceDefinition", false, true)]
    [TestCase("Constrained", "BaseDefinition", true)]
    [TestCase("TransitiveConstraint", "BaseDefinition", true)]
    [TestCase("TaskInt", "TaskDefinition", true)]
    [TestCase("DerivedTask", "TaskDefinition", true)]
    [TestCase("TaskInt", "TaskInt", true)]
    [TestCase("TaskInt", "Object", true)]
    public async Task DerivesFrom_Respects_Generic_Definitions_And_Constructed_Arguments(
        string source, string candidate, bool expected, bool baseTypesOnly = false)
    {
        var types = await CreateTypes();
        Assert.That(types[source].DerivesFrom(types[candidate], baseTypesOnly), Is.EqualTo(expected));
    }

    [Test]
    public async Task DerivesFrom_Rejects_Missing_Types()
    {
        var types = await CreateTypes();
        ITypeSymbol? missing = null;
        Assert.That(missing.DerivesFrom(types["Object"]), Is.False);
        Assert.That(types["Object"].DerivesFrom(missing), Is.False);
    }

    private static async Task<Dictionary<string, ITypeSymbol>> CreateTypes()
    {
        var references = await ReferenceAssemblies.Net.Net80.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var compilation = CSharpCompilation.Create("TypeRelationships",
            new[] { CSharpSyntaxTree.ParseText(@"
public interface IMarker<T> { }
public class GenericBase<T> : IMarker<T> { }
public class Derived : GenericBase<int> { }
public class Constraints<T, U> where T : GenericBase<int> where U : T { }
public class DerivedTask : System.Threading.Tasks.Task<int>
{
    public DerivedTask() : base(() => 42) { }
}") }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.That(compilation.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty);

        var integer = compilation.GetSpecialType(SpecialType.System_Int32);
        var text = compilation.GetSpecialType(SpecialType.System_String);
        var baseDefinition = compilation.GetTypeByMetadataName("GenericBase`1")!;
        var interfaceDefinition = compilation.GetTypeByMetadataName("IMarker`1")!;
        var taskDefinition = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1")!;
        var constraints = compilation.GetTypeByMetadataName("Constraints`2")!;
        return new Dictionary<string, ITypeSymbol>
        {
            ["BaseDefinition"] = baseDefinition,
            ["BaseInt"] = baseDefinition.Construct(integer),
            ["BaseString"] = baseDefinition.Construct(text),
            ["Derived"] = compilation.GetTypeByMetadataName("Derived")!,
            ["InterfaceDefinition"] = interfaceDefinition,
            ["InterfaceInt"] = interfaceDefinition.Construct(integer),
            ["InterfaceString"] = interfaceDefinition.Construct(text),
            ["Constrained"] = constraints.TypeParameters[0],
            ["TransitiveConstraint"] = constraints.TypeParameters[1],
            ["TaskDefinition"] = taskDefinition,
            ["TaskInt"] = taskDefinition.Construct(integer),
            ["DerivedTask"] = compilation.GetTypeByMetadataName("DerivedTask")!,
            ["Object"] = compilation.GetSpecialType(SpecialType.System_Object),
        };
    }
}

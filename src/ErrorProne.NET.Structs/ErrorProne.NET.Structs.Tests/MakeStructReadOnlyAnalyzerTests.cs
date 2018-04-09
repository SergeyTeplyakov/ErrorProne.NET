using System.Collections.Generic;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Structs.Test
{
    [TestFixture]
    public class MakeStructReadOnlyAnalyzerTests : CSharpAnalyzerTestFixture<MakeStructReadOnlyAnalyzer>
    {
        public const string DiagnosticId = MakeStructReadOnlyAnalyzer.DiagnosticId;

        [Test]
        public void HasDiagnosticsForEmptyStruct()
        {
            string code = @"struct [|FooBar|] {}";
            HasDiagnostic(code, DiagnosticId);
        }

        [TestCaseSource(nameof(GetHasDiagnosticCases))]
        public void HasDiagnosticCases(string code)
        {
            HasDiagnostic(code, DiagnosticId);
        }

        public static IEnumerable<string> GetHasDiagnosticCases()
        {
            // With constructor only
            yield return 
@"struct [|FooBar|] {
    public FooBar(int n):this() {}
}";

            // With one private field
            yield return
@"struct [|FooBar|] {
    private readonly int _x;
    public FooBar(int x) => _x = x;
}";
            
            // With one public readonly property
            yield return
@"struct [|FooBar|] {
    public int X {get;}
    public FooBar(int x) => X = x;
}";
            
            // With one public readonly property and method
            yield return
@"struct [|FooBar|] {
    public int X {get;}
    public FooBar(int x) => X = x;
    public int GetX() => X;
}";

        }

        [Test]
        public void NoDiagnosticCasesWhenStructIsAlreadyReadonly()
        {
            string code = @"readonly struct FooBar {}";
            NoDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void NoDiagnosticCasesWhenStructIsAlreadyReadonlyWithPartialDeclaation()
        {
            string code = @"partial struct FooBar {} readonly partial struct FooBar {}";
            NoDiagnostic(code, DiagnosticId);
        }

        [TestCaseSource(nameof(GetNoDiagnosticCases))]
        public void NoDiagnosticCases(string code)
        {
            NoDiagnostic(code, DiagnosticId);
        }

        public static IEnumerable<string> GetNoDiagnosticCases()
        {
            // Already marked with 
            yield return 
@"readonly struct FooBar {
    public FooBar(int n):this() {}
}";

            // Non-readonly field
            yield return
@"struct FooBar {
    private int _x;
}";
            
            // With a setter
            yield return
@"struct FooBar {
    public int X {get; private set;}
}";

        }
    }
}
using System.Collections.Generic;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Structs.Test
{
    [TestFixture]
    public class UseInModifierForReadOnlyStructAnalyzerTests : CSharpAnalyzerTestFixture<UseInModifierForReadOnlyStructAnalyzer>
    {
        public const string DiagnosticId = UseInModifierForReadOnlyStructAnalyzer.DiagnosticId;

        [Test]
        public void HasDiagnosticsForReadOnlyStruct()
        {
            string code = @"readonly struct S {} class FooBar { public void Foo([|S n|]) {} }";
            HasDiagnostic(code, DiagnosticId);
        }

        [TestCaseSource(nameof(GetNoDiagnosticsTestCases))]
        public void NoDiagnosticsTestCases(string code)
        {
            NoDiagnostic(code, DiagnosticId);
        }

        public static IEnumerable<string> GetNoDiagnosticsTestCases()
        {
            // Passed by value
            yield return @"struct S {} class FooBar { public void Foo(S n) {} }";
            
            // Passed by ref
            yield return @"struct S {} class FooBar { public void Foo(ref S n) {} }";
            
            // Passed by out
            yield return @"struct S {} class FooBar { public void Foo(out S n) {n = default(S);} }";
            
            // ReadOnly struct passed by in
            yield return @"readonly struct S {} class FooBar { public void Foo(in S n) {} }";
        }
    }
}
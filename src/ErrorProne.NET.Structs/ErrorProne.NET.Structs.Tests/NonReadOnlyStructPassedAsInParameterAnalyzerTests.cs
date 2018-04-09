using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Structs.Test
{
    [TestFixture]
    public class NonReadOnlyStructPassedAsInParameterAnalyzerTests : CSharpAnalyzerTestFixture<NonReadOnlyStructPassedAsInParameterAnalyzer>
    {
        public const string DiagnosticId = NonReadOnlyStructPassedAsInParameterAnalyzer.DiagnosticId;

        [Test]
        public void HasDiagnosticsForInt()
        {
            string code = @"class FooBar { public void Foo([|in int n|]) {} }";
            HasDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void HasDiagnosticsForIntAndCustomStruct()
        {
            string code = @"struct S {} class FooBar { public void Foo([|in int n|], [|in S s|]) {} }";
            HasDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void HasDiagnosticsForEmptyStruct()
        {
            string code = @"struct S {} class FooBar { public void Foo([|in S n|]) {} }";
            HasDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void NoDiagnosticsForReadOnlyStruct()
        {
            string code = @"readonly struct S {} class FooBar { public void Foo(in S n) {} }";
            NoDiagnostic(code, DiagnosticId);
        }
    }
}
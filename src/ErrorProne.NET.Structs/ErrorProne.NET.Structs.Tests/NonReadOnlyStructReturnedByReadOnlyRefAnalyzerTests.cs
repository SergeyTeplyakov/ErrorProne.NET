using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Structs.Test
{
    [TestFixture]
    public class NonReadOnlyStructReturnedByReadOnlyRefAnalyzerTests : CSharpAnalyzerTestFixture<NonReadOnlyStructReturnedByReadOnlyRefAnalyzer>
    {
        public const string DiagnosticId = NonReadOnlyStructReturnedByReadOnlyRefAnalyzer.DiagnosticId;

        [Test]
        public void HasDiagnosticsForInt()
        {
            string code = @"class FooBar { public [|ref readonly int |]Foo() => throw new System.Exception(); }";
            HasDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void HasDiagnosticsForCustomStruct()
        {
            string code = @"struct S {public void Foo() {}} class FooBar { public [|ref readonly S |]Foo() => throw new System.Exception(); }";
            HasDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void NoDiagnosticsForReadOnlyStruct()
        {
            string code = @"readonly struct S {} class FooBar { public ref readonly S Foo() => throw new System.Exception(); }";
            NoDiagnostic(code, DiagnosticId);
        }

        // [Test] // not implemented yet.
        public void HasDiagnosticsWhenNonReadOnlyStructIsUsedWithGenericMethodThatPassesTByIn()
        {
            string code = @"
struct S {}
class FooBar
{
    public static void Foo<T>(in T t) {}
    public static void Usage(int n) => Foo<int>([|n|]);
}";
            HasDiagnostic(code, DiagnosticId);
        }
    }
}
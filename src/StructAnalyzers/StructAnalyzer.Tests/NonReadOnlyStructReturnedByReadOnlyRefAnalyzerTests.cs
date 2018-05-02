using System.Collections.Generic;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Structs.Tests
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

        [TestCaseSource(nameof(GetHasDiagnosticsTestCases))]
        public void HasDiagnosticsTestCases(string code)
        {
            HasDiagnostic(code, DiagnosticId);
        }

        public static IEnumerable<string> GetHasDiagnosticsTestCases()
        {
            // Has diagnostics for ref return property
            yield return
                @"struct S {public void Foo() {} } class FooBar {private S _s; public [|ref readonly S |]S => _s; }";
            
            // Has diagnostics for ref return method with expression body
            yield return
                @"struct S {public void Foo() {} } class FooBar {private S _s; public [|ref readonly S |]S() => _s; }";
                    }

        [TestCaseSource(nameof(GetNoDiagnosticsTestCases))]
        public void NoDiagnosticsTestCases(string code)
        {
            NoDiagnostic(code, DiagnosticId);
        }

        public static IEnumerable<string> GetNoDiagnosticsTestCases()
        {
            // No diagnostics for ref return property
            yield return
                @"struct S {} class FooBar {private S _s; public ref readonly S S => _s; }";
            
            // No diagnostics for ref return method with expression body
            yield return
                @"struct S {} class FooBar {private S _s; public ref readonly S S() => _s; }";
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
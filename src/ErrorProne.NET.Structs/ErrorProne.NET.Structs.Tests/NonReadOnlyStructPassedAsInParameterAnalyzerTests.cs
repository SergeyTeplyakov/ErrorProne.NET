using System.Collections.Generic;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Structs.Tests
{
    [TestFixture]
    public class NonReadOnlyStructPassedAsInParameterAnalyzerTests : CSharpAnalyzerTestFixture<NonReadOnlyStructPassedAsInParameterAnalyzer>
    {
        public const string DiagnosticId = NonReadOnlyStructPassedAsInParameterAnalyzer.DiagnosticId;

        [Test]
        public void HasDiagnosticsForInt()
        {
            // This is actually potentially dangerous case, because people may pass
            // primitives by in just for the sake of readability.
            string code = @"class FooBar { public void Foo([|in int n|]) {} }";
            HasDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void HasDiagnosticsForLocalMethod()
        {
            string code = @"
struct S {
    public void Foo()
    {
        void ByIn([|in S s|]) {}
    }
}";
            HasDiagnostic(code, DiagnosticId);
        }

        [TestCaseSource(nameof(GetHasDiagnosticsTestCases))]
        public void HasDiagnosticsTestCases(string code)
        {
            HasDiagnostic(code, DiagnosticId);
        }

        public static IEnumerable<string> GetHasDiagnosticsTestCases()
        {
            // Diagnostic for struct with one method
            yield return @"struct S { public void Foo() {} static void ByIn([|in S s|]) {} }";
            
            // Diagnostic for struct with methods and props
            yield return @"struct S { internal void Foo() {} internal int X {get;} static void ByIn([|in S s|]) {} }";

            // For custom struct and int
            yield return @"struct S {public void Foo() {}} class FooBar { public void Foo([|in int n|], [|in S s|]) {} }";

            // For generic struct
            yield return @"struct S<T> {public void Foo() {}} class FooBar<T> {public void Foo([|in S<T> s|]) {}";

            // Non readonly struct without fields used in the struct
            yield return @"struct S {private int S {get;} public void Foo([|in S s|]) {} }";
        }

        [TestCaseSource(nameof(GetNoDiagnosticsTestCases))]
        public void NoDiagnosticsTestCases(string code)
        {
            NoDiagnostic(code, DiagnosticId);
        }

        public static IEnumerable<string> GetNoDiagnosticsTestCases()
        {
            // Not readonly struct with fields used within the struct
            yield return @"struct S {private int _s; public void Foo(in S s) {} }";

            // No diagnostics for readonly struct
            yield return @"readonly struct S { public void Foo(in S s) {} }";

            // No diagnostics for empty struct
            yield return @"struct S {} class FooBar { public void Foo(in S n) {} }";

            // No diagnostics for struct with static members
            yield return @"struct S { static void Foo() {}} class FooBar { public void Foo(in S n) {} }";
            
            // No diagnostics for POCO struct
            yield return @"struct S {public int x; } class FooBar { public void Foo(in S n) {} }";

            // No diagnostics for mixed struct
            yield return @"struct S {public int x; public void Foo() {} } class FooBar { public void Foo(in S n) {} }";

            // No diagnostics for tuples
            yield return @"class FooBar { public void Foo(in (int x, int y) t) {}";
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
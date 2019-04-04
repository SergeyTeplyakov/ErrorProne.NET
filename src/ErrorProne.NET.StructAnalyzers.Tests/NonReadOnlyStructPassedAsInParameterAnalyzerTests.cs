using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Collections.Generic;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.StructAnalyzers.NonReadOnlyStructPassedAsInParameterAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.StructAnalyzers.Tests
{
    [TestFixture]
    public class NonReadOnlyStructPassedAsInParameterAnalyzerTests
    {
        [Test]
        public async Task HasDiagnosticsForInt()
        {
            // This is actually potentially dangerous case, because people may pass
            // primitives by in just for the sake of readability.
            string code = @"class FooBar { public void Foo([|in int n|]) {} }";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task NoFailuresOnPartiallyValidCode()
        {
            // There was a bug, that caused IndexOutOfRange exception when parameter name was missing
            string code = @"class FooBar { public void Foo(in int[||]) {} }";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task HasDiagnosticsForLocalMethod()
        {
            string code = @"
struct S {
    public void Foo()
    {
        void ByIn([|in S s|]) {}
    }
}";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [TestCaseSource(nameof(GetHasDiagnosticsTestCases))]
        public async Task HasDiagnosticsTestCases(string code)
        {
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        public static IEnumerable<string> GetHasDiagnosticsTestCases()
        {
            // The warning should be only in the base class
            yield return @"
struct S { public void Foo() {} } 
class B {public virtual void Foo([|in S s|]) {}}
class D : B {public override void Foo(in S s) {}}";
            
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
        public async Task NoDiagnosticsTestCases(string code)
        {
            await VerifyCS.VerifyAnalyzerAsync(code);
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

        [Test] // not implemented yet.
        public async Task HasDiagnosticsWhenNonReadOnlyStructIsUsedWithGenericMethodThatPassesTByIn()
        {
            string code = @"
struct S {}
class FooBar
{
    public static void Foo<T>(in T t) {}
    public static void Usage(int n) => Foo<int>([|n|]);
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }
    }
}
using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Collections.Generic;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.StructAnalyzers.NonReadOnlyStructReturnedByReadOnlyRefAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.StructAnalyzers.Tests
{
    [TestFixture]
    public class NonReadOnlyStructReturnedByReadOnlyRefAnalyzerTests
    {
        [Test]
        public async Task HasDiagnosticsForInt()
        {
            string code = @"class FooBar { public [|ref readonly int |]Foo() => throw new System.Exception(); }";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task HasDiagnosticsForCustomStruct()
        {
            string code = @"struct S {public void Foo() {}} class FooBar { public [|ref readonly S |]Foo() => throw new System.Exception(); }";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task HasDiagnosticsForLocalFunction()
        {
            string code = @"
struct S { public void Foo() { } }
class FooBar
{
    private S _s;
    public void F()
    {
        [|ref readonly S|] S2() => ref _s;
    }
}";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task NoDiagnosticsForReadOnlyStruct()
        {
            string code = @"readonly struct S {} class FooBar { public ref readonly S Foo() => throw new System.Exception(); }";
            await VerifyCS.VerifyAnalyzerAsync(code);
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
            // Has diagnostics for ref return property
            yield return
                @"struct S {public void Foo() {} } class FooBar {private S _s; public [|ref readonly S |]S => {|CS8150:_s|}; }";
            
            // Has diagnostics for ref return method with expression body
            yield return
                @"struct S {public void Foo() {} } class FooBar {private S _s; public [|ref readonly S |]S() => {|CS8150:_s|}; }";
        }

        [TestCaseSource(nameof(GetNoDiagnosticsTestCases))]
        public async Task NoDiagnosticsTestCases(string code)
        {
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        public static IEnumerable<string> GetNoDiagnosticsTestCases()
        {
            // No diagnostics for ref return property
            yield return
                @"struct S {} class FooBar {private S _s; public ref readonly S S => {|CS8150:_s|}; }";
            
            // No diagnostics for ref return method with expression body
            yield return
                @"struct S {} class FooBar {private S _s; public ref readonly S S() => {|CS8150:_s|}; }";
        }

        [Test]
        [Ignore("not implemented yet")]
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
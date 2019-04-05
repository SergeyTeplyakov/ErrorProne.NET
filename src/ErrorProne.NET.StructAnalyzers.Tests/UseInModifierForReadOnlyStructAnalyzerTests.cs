﻿using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Collections.Generic;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.StructAnalyzers.UseInModifierForReadOnlyStructAnalyzer,
    ErrorProne.NET.StructAnalyzers.UseInModifierForReadOnlyStructCodeFixProvider>;

namespace ErrorProne.NET.StructAnalyzers.Tests
{
    [TestFixture]
    public class UseInModifierForReadOnlyStructAnalyzerTests
    {
        [Test]
        public async Task NoDiagnosticsForAsyncMethod()
        {
            string code = @"readonly struct S {public async void Foo(S s) {} }";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task NoDiagnosticsForIteratorBlock()
        {
            string code = @"readonly struct S {public System.Collections.Generic.IEnumerable<int> Foo(S s) {yield break;} }";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task NoDiagnosticsForReadOnlyStruct()
        {
            string code = @"readonly struct S {} class FooBar { public void Foo(S n) {} }";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task HasDiagnosticsForLargeReadOnlyStruct()
        {
            string code = @"readonly struct S {readonly long l, l2;} class FooBar { public void Foo([|S n|]) {} }";
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
            // Expression body for method
            yield return @"readonly struct FooBar {readonly long l, l2; public static System.Func<FooBar> Foo([|FooBar fb|]) => null; }";
            
            // Diagnostic for extension method.
            yield return @"readonly struct FooBar {readonly long l, l2;} static class Extensions { public static string Foo([|this FooBar fb|]) => null; }";

            // Diagnostic for a delegate
            yield return @"readonly struct S { readonly long l, l2; } delegate void Foo([|S s|]);";
            
            // Diagnostic for an indexer
            // TODO: the location of the diagnostic is weird, because DeclaredSyntaxReferences for the parameter in this case is empty:(
            yield return @"readonly struct S { readonly long l,l2; public int this[S [|s|]] => 42; }";

            // Diagnostic on abstract declaration, but on the overloaded
            yield return
                @"readonly struct S { readonly long l,l2; }
abstract class B {public virtual void Foo([|S s|]) {}}
class D : B {public override void Foo(S s) {}}
";

            // Diagnostic for local function: not supported yet!
            // yield return @"readonly struct S { } class FooBar { public void Foo() { void Local([|S s|]) {} } }";
        }

        [TestCaseSource(nameof(GetNoDiagnosticsTestCases))]
        public async Task NoDiagnosticsTestCases(string code)
        {
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        public static IEnumerable<string> GetNoDiagnosticsTestCases()
        {
            // No diagnostic for property with a setter
            yield return @"
readonly struct Foo { private readonly long l1, l2; }
class FooBar { public Foo F {get;set;} }";
            
            // No diagnostic if returns Task
            yield return @"
readonly struct Foo {
  public System.Threading.Tasks.Task FooAsync(Foo f) => null;
}";
            // No diagnostic if returns Task<T>
            yield return @"
readonly struct Foo {
  public System.Threading.Tasks.Task<int> FooAsync(Foo f) => null;
}";
            
            // No diagnostic if returns ValueTask<T>
            yield return @"
readonly struct Foo {
  public System.Threading.Tasks.ValueTask<int> FooAsync(Foo f) => default;
}";

            // No diagnostic if implements interface
            yield return
@"readonly struct Foo : System.IEquatable<Foo>
{
    public bool Equals(Foo other) => true;
}";

            // No diagnostic if implements interface implicitely
            yield return
@"readonly struct Foo : System.IEquatable<Foo>
{
    bool System.IEquatable<Foo>.Equals(Foo other) => true;
}";

            // Passed by value
            yield return @"struct S {} class FooBar { public void Foo(S n) {} }";
            
            // Passed by ref
            yield return @"struct S {} class FooBar { public void Foo(ref S n) {} }";
            
            // Passed by out
            yield return @"struct S {} class FooBar { public void Foo(out S n) {n = default(S);} }";
            
            // ReadOnly struct passed by in
            yield return @"readonly struct S {} class FooBar { public void Foo(in S n) {} }";

            // No diagnostics for constructs. Most likely the value would be copied into an internal state any way.
            yield return @"readonly struct S{} class FooBar { public FooBar(S s) {} }";
            
            // No diagnostics for operators.
            yield return @"
public readonly struct S
{
    public static bool operator ==(S left, S right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(S left, S right)
    {
        return !left.Equals(right);
    }
}";
        }
    }
}
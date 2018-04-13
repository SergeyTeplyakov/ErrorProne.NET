using System;
using System.Collections.Generic;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Structs.Tests
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

        [TestCaseSource(nameof(GetHasDiagnosticsTestCases))]
        public void HasDiagnosticsTestCases(string code)
        {
            HasDiagnostic(code, DiagnosticId);
        }

        public static IEnumerable<string> GetHasDiagnosticsTestCases()
        {
            // Diagnostic for a delegate
            yield return @"readonly struct S { } delegate void Foo([|S s|]);";

            // Diagnostic on abstract declaration, but on the overloaded
            yield return
                @"readonly struct S {}
abstract class B {public virtual void Foo([|S s|]) {}}
class D : B {public override void Foo(S s) {}
";

            // Diagnostic for local function: not supported yet!
            // yield return @"readonly struct S { } class FooBar { public void Foo() { void Local([|S s|]) {} } }";
        }

        [TestCaseSource(nameof(GetNoDiagnosticsTestCases))]
        public void NoDiagnosticsTestCases(string code)
        {
            NoDiagnostic(code, DiagnosticId);
        }
        
        public static IEnumerable<string> GetNoDiagnosticsTestCases()
        {
            // No diagnostic if implements interface
            yield return
                @"readonly struct Foo : System.IEquatable<Foo>
{
    public bool Equals(Foo other) => true;
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
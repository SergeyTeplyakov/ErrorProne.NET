using System;
using System.Collections.Generic;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Structs.Tests
{
    [TestFixture]
    public class HiddenStructCopyAnalyzerTests : CSharpAnalyzerTestFixture<HiddenStructCopyAnalyzer>
    {
        public const string DiagnosticId = HiddenStructCopyAnalyzer.DiagnosticId;

        [Test]
        public void HasDiagnosticsForMethodCallsOnReadOnlyField()
        {
            string code = @"struct S {public int Foo() => 42;} class Foo {private readonly S _s; public int Bar() => [|_s|].Foo();";
            HasDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void HasDiagnosticsForMethodCallsOnInParameter()
        {
            string code = @"struct S {public int Foo() => 42;} class Foo {public int Bar(in S s) => [|s|].Foo();}";
            HasDiagnostic(code, DiagnosticId);
        }
        
        [Test]
        public void HasDiagnosticsForMethodCallsOnRefReadOnly()
        {
            string code = @"
struct S { public int Foo() => 42; }
class Foo {
    public int Bar(S s)
    {
        ref readonly var rs = ref s;
        return [|rs|].Foo();
    }
}";
            HasDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void HasDiagnosticsForIndexerOnReadOnlyField()
        {
            string code = @"
struct S {
    public string this[int x] => string.Empty;
}
public class C {
    private readonly S _s;
    public string M() => [|_s|][0];
}"; ;
            HasDiagnostic(code, DiagnosticId);
        }

        [TestCaseSource(nameof(GetHasDiagnosticCases))]
        public void HasDiagnosticCases(string code)
        {
            HasDiagnostic(code, DiagnosticId);
        }

        public static IEnumerable<string> GetHasDiagnosticCases()
        {
            // On for properties
            yield return "struct S {public int X => 42;} class Foo {private readonly S _s; public int Bar() => [|_s|].X;";
            
            // On for methods
            yield return "struct S {public int X() => 42;} class Foo {private readonly S _s; public int Bar() => [|_s|].X();";

            // On for indexers
            yield return @"
struct S {
    public string this[int x] => string.Empty;
}
public class C {
    private readonly S _s;
    public string M() => [|_s|][0];
}";
        }
        
        [TestCaseSource(nameof(GetNoDiagnosticCases))]
        public void NoDiagnosticCases(string code)
        {
            NoDiagnostic(code, DiagnosticId);
        }

        public static IEnumerable<string> GetNoDiagnosticCases()
        {
            // No diagnostics if a struct is readonly
            yield return "readonly struct S {public int X => 42;} class Foo {private readonly S _s; public int Bar() => _s.X;";

            // No diagnostics if field accessed
            yield return "struct S {public int X;} class Foo {private readonly S _s; public int Bar() => _s.X;";
        }
    }
}
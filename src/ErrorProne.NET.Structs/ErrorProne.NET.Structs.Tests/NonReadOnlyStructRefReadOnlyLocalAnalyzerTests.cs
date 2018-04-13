using System.Collections.Generic;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Structs.Tests
{
    [TestFixture]
    public class NonReadOnlyStructRefReadOnlyLocalAnalyzerTests : CSharpAnalyzerTestFixture<NonReadOnlyStructRefReadOnlyLocalAnalyzer>
    {
        public const string DiagnosticId = NonReadOnlyStructRefReadOnlyLocalAnalyzer.DiagnosticId;

        [Test]
        public void HasDiagnosticsForNonReadOnlyStruct()
        {
            string code = @"struct S {public void Foo(S s) { [|ref readonly var|] rs = ref s;} }";
            HasDiagnostic(code, DiagnosticId);
        }

        [TestCaseSource(nameof(GetHasDiagnosticsTestCases))]
        public void HasDiagnosticsTestCases(string code)
        {
            HasDiagnostic(code, DiagnosticId);
        }

        public static IEnumerable<string> GetHasDiagnosticsTestCases()
        {
            yield break;
        }

        [TestCaseSource(nameof(GetNoDiagnosticsTestCases))]
        public void NoDiagnosticsTestCases(string code)
        {
            NoDiagnostic(code, DiagnosticId);
        }

        public static IEnumerable<string> GetNoDiagnosticsTestCases()
        {
            yield break;
        }
    }
}
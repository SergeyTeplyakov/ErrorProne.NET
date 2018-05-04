using System.Collections.Generic;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Structs.Tests
{
    [TestFixture]
    public class HashTableIncompatibilityAnalyzerTests : CSharpAnalyzerTestFixture<HashTableIncompatibilityAnalyzer>
    {
        public const string DiagnosticId = HashTableIncompatibilityAnalyzer.DiagnosticId;

        [Test]
        public void WarnForHashSet()
        {
            string code = @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.HashSet<MyStruct>|] hs = null;
}";
            HasDiagnostic(code, DiagnosticId);
        }

        [TestCaseSource(nameof(GetHasDiagnosticCases))]
        public void HasDiagnosticCases(string code)
        {
            HasDiagnostic(code, DiagnosticId);
        }

        public static IEnumerable<string> GetHasDiagnosticCases()
        {
            yield break;
        }
        
        [TestCaseSource(nameof(GetNoDiagnosticCases))]
        public void NoDiagnosticCases(string code)
        {
            NoDiagnostic(code, DiagnosticId);
        }
        
        public static IEnumerable<string> GetNoDiagnosticCases()
        {
            yield break;
        }
    }
}
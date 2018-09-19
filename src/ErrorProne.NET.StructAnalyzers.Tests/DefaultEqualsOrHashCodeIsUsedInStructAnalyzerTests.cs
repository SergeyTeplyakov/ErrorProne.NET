using System.Collections.Generic;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Structs.Tests
{
    [TestFixture]
    public class DefaultEqualsOrHashCodeIsUsedInStructAnalyzerTests : CSharpAnalyzerTestFixture<DefaultEqualsOrHashCodeIsUsedInStructAnalyzer>
    {
        public const string DiagnosticId = DefaultEqualsOrHashCodeIsUsedInStructAnalyzer.DiagnosticId;
                
        [TestCaseSource(nameof(GetHasDiagnosticCases))]
        public void HasDiagnosticCases(string code)
        {
            HasDiagnostic(code, DiagnosticId);
        }

        public static IEnumerable<string> GetHasDiagnosticCases()
        {
            // Warn when used in another class
            yield return @"
struct MyStruct {}
class AnotherStruct { private MyStruct ms; public override int GetHashCode() => ms.[|GetHashCode|]() ^ 42; }
";
            
            // Warn when used in another struct
            yield return @"
struct MyStruct {}
struct AnotherStruct { private MyStruct ms; public override int GetHashCode() => ms.[|GetHashCode|]() ^ 42; }
";
            
            // Warn when used in another struct
            yield return @"
struct MyStruct {}
struct AnotherStruct { private MyStruct ms; public override bool Equals(object other) => ms.[|Equals|](other); }
";
            
            // Warn When Equals is used for implementing IEquatable
            yield return @"
struct MyStruct { }
struct AnotherStruct : System.IEquatable<AnotherStruct>
{
    private MyStruct ms;
    public override bool Equals(object other) => false;
    public bool Equals(AnotherStruct another) => ms.[|Equals|](another);
}
";
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
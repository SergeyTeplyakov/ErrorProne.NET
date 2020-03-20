using ErrorProne.NET.TestHelpers;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.StructAnalyzers.DefaultEqualsOrHashCodeIsUsedInStructAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.StructAnalyzers.Tests
{
    [TestFixture]
    public class DefaultEqualsOrHashCodeIsUsedInStructAnalyzerTests
    {
        [TestCaseSource(nameof(GetHasDiagnosticCases))]
        public async Task HasDiagnosticCases(string code)
        {
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
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
        public async Task NoDiagnosticCases(string code)
        {
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        public static IEnumerable<string> GetNoDiagnosticCases()
        {
            yield break;
        }
    }
}
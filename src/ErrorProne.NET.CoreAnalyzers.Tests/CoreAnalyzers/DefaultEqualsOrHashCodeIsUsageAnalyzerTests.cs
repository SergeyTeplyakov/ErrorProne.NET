using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.DefaultEqualsOrHashCodeUsageAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.CoreAnalyzers
{
    [TestFixture]
    public class DefaultEqualsOrHashCodeIsUsageAnalyzerTests
    {
        [TestCaseSource(nameof(GetHasDiagnosticCases))]
        public async Task HasDiagnosticCases(string code)
        {
            await VerifyCS.VerifyAsync(code);
        }

        public static IEnumerable<string> GetHasDiagnosticCases()
        {
            // Warn when used in another class
            yield return @"
struct MyStruct {}
class AnotherStruct { private MyStruct ms; public override int GetHashCode() => [|ms.GetHashCode()|] ^ 42; }
";
            
            // Warn when used in another struct
            yield return @"
struct MyStruct {}
struct AnotherStruct { private MyStruct ms; public override int GetHashCode() => [|ms.GetHashCode()|] ^ 42; }
";
            
            // Warn when used in another struct
            yield return @"
struct MyStruct {}
struct AnotherStruct { private MyStruct ms; public override bool Equals(object other) => [|ms.Equals(other)|]; }
";
            
            // Warn When Equals is used for implementing IEquatable
            yield return @"
struct MyStruct { }
struct AnotherStruct : System.IEquatable<AnotherStruct>
{
    private MyStruct ms;
    public override bool Equals(object other) => false;
    public bool Equals(AnotherStruct another) => [|ms.Equals(another)|];
}
";
            
            // Warn When Equals is used in another position
            yield return @"
struct MyStruct { }
static class Example
{
    public static bool Test(MyStruct ms)
    {
        return [|ms.Equals(ms)|];
    }
}
";
            
            // Warn When GetHashCode is used in another position
            yield return @"
struct MyStruct { }
static class Example
{
    public static int Test(MyStruct ms) => [|ms.GetHashCode()|];
}
";
        }
    }
}
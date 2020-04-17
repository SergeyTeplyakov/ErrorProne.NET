using ErrorProne.NET.TestHelpers;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.StructAnalyzers.NonReadOnlyStructRefReadOnlyLocalAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.StructAnalyzers.Tests
{
    [TestFixture]
    public class NonReadOnlyStructRefReadOnlyLocalAnalyzerTests
    {
        [Test]
        public async Task HasDiagnosticsForNonReadOnlyStruct()
        {
            string code = @"struct S {public void Foo(S s) { [|ref readonly var|] rs = ref s;} }";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
    }
}
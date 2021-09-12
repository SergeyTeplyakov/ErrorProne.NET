using ErrorProne.NET.TestHelpers;
using NUnit.Framework;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.Net.StructAnalyzers.NonDefaultStructs.NonDefaultableStructDeclarationAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class NonDefaultableStructDeclarationTests
    {
        // TODO: add support for record structs once available.
        [Test]
        public async Task Warn_If_Constructor_Is_Missing()
        {
            string code = @"
[NonDefaultableAttribute]
public struct [|MyS|] { }
";

            await VerifySource(code);
        }
        
        [Test]
        public async Task No_Warn_If_Constructor_Is_Present()
        {
            string code = @"
[NonDefaultableAttribute]
public struct MyS { public MyS(int x) {} }
";

            await VerifySource(code);
        }

        private static async Task VerifySource(string code)
        {
            await new VerifyCS.Test
                {
                    TestState =
                    {
                        Sources = {code},
                    },
                    LanguageVersion = LanguageVersion.Latest,
                }
                .WithNonDefaultableAttribute()
                .WithoutGeneratedCodeVerification()
                .RunAsync();
        }
    }
}
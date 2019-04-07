using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.NullConditionalOperatorAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class NullConditionalOperatorAnalyzerTests
    {
        [Test]
        public async Task Warn_For_Null_Conditional()
        {
            string code = @"
public class MyClass
{
    public async System.Threading.Tasks.Task Foo(MyClass m)
    {
       [|await m?.Foo(null)|];
    }
}
";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
   }
}
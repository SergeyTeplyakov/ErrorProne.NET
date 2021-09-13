using ErrorProne.NET.TestHelpers;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.SuspiciousExceptionHandlingAnalyzer,
    ErrorProne.NET.CoreAnalyzers.ExceptionHandlingFixers>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.SuspiciousExceptionHandling
{
    [TestFixture]
    public class RemoveExMessageCodeFixProviderTests
    {
        [Test]
        public async Task RemoveExMessage()
        {
            string code = @"
class FooBar {
  public static void Foo() {
    try { new object(); }
    catch(System.Exception e) {System.Console.WriteLine(e.Message);}
  }
}";

            string expected = @"
class FooBar {
  public static void Foo() {
    try { new object(); }
    catch(System.Exception e) {System.Console.WriteLine(e);}
  }
}";

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(SuspiciousExceptionHandlingAnalyzer.Rule).WithSpan(5, 59, 5, 66).WithArguments("e"),
                    },
                },
                FixedState =
                {
                    Sources = { expected },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
    }
}
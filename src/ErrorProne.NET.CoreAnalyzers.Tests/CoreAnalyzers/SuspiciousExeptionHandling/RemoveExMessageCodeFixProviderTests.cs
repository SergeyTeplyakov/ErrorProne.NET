using Microsoft.CodeAnalysis;
using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.SuspiciousExceptionHandlingAnalyzer,
    ErrorProne.NET.CoreAnalyzers.ExceptionHandlingFixers>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.SuspiciousExeptionHandling
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
                        VerifyCS.Diagnostic(SuspiciousExceptionHandlingAnalyzer.DiagnosticDescriptor).WithSpan(5, 57, 5, 58).WithArguments("e"),
                        VerifyCS.Diagnostic(SuspiciousExceptionHandlingAnalyzer.DiagnosticDescriptor).WithSeverity(DiagnosticSeverity.Hidden).WithSpan(5, 59, 5, 66).WithMessage("bar"),
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
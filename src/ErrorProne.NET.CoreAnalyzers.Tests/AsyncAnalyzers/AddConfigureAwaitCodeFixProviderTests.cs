using ErrorProne.NET.AsyncAnalyzers;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.AddConfigureAwaitAnalyzer,
    ErrorProne.NET.AsyncAnalyzers.AddConfigureAwaitCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class AddConfigureAwaitCodeFixProviderTests
    {
        [Test]
        public async Task AddConfigureAwaitFalse()
        {
            string code = @"
[assembly:UseConfigureAwaitFalse()]
public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42);
    }
}";

            string expected = @"
[assembly:UseConfigureAwaitFalse()]
public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42).ConfigureAwait(false);
    }
}";

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        DiagnosticResult.CompilerError("CS0246").WithSpan(2, 11, 2, 33).WithMessage("The type or namespace name 'UseConfigureAwaitFalseAttribute' could not be found (are you missing a using directive or an assembly reference?)"),
                        DiagnosticResult.CompilerError("CS0246").WithSpan(2, 11, 2, 33).WithMessage("The type or namespace name 'UseConfigureAwaitFalse' could not be found (are you missing a using directive or an assembly reference?)"),
                        VerifyCS.Diagnostic(AddConfigureAwaitAnalyzer.Rule).WithSpan(7, 8, 7, 51),
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
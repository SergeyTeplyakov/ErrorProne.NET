using ErrorProne.NET.AsyncAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.RemoveConfigureAwaitAnalyzer,
    ErrorProne.NET.AsyncAnalyzers.RemoveConfigureAwaitCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class RemoveConfigureAwaitAnalyzerTests
    {
        [Test]
        public async Task Warn_For_Task_Delay()
        {
            string code = @"
[assembly:DoNotUseConfigureAwait()]

public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42).ConfigureAwait(false);
    }
}
";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(RemoveConfigureAwaitAnalyzer.Rule).WithSeverity(DiagnosticSeverity.Hidden).WithSpan(8, 52, 8, 73).WithMessage("bar"),
                    },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task Warn_For_Property()
        {
            string code = @"
[assembly:DoNotUseConfigureAwait()]

public class MyClass
{
    private static System.Threading.Tasks.Task MyTask => null;
    public static async System.Threading.Tasks.Task Foo()
    {
       await MyTask.ConfigureAwait(false);
    }
}
";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(RemoveConfigureAwaitAnalyzer.Rule).WithSeverity(DiagnosticSeverity.Hidden).WithSpan(9, 21, 9, 42).WithMessage("bar"),
                    },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
    }
}
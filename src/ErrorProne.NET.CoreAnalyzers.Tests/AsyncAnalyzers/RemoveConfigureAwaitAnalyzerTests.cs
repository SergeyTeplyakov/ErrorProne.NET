using ErrorProne.NET.AsyncAnalyzers;
using ErrorProne.NET.TestHelpers;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.RedundantConfigureAwaitFalseAnalyzer,
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
                        VerifyCS.Diagnostic(RedundantConfigureAwaitFalseAnalyzer.Rule).WithSpan(8, 52, 8, 73),
                    },
                },
            }.WithoutGeneratedCodeVerification().WithConfigureAwaitAttributes().RunAsync();
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
                        VerifyCS.Diagnostic(RedundantConfigureAwaitFalseAnalyzer.Rule).WithSpan(9, 21, 9, 42),
                    },
                },
            }.WithoutGeneratedCodeVerification().WithConfigureAwaitAttributes().RunAsync();
        }
    }
}
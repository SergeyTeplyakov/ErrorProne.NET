using ErrorProne.NET.AsyncAnalyzers;
using ErrorProne.NET.TestHelpers;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.ConfigureAwaitRequiredAnalyzer,
    ErrorProne.NET.AsyncAnalyzers.AddConfigureAwaitCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class AddConfigureAwaitAnalyzerTests
    {
        [Test]
        public async Task Warn_For_Task_Delay()
        {
            string code = @"
[assembly:UseConfigureAwaitFalse()]

public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42);
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
                        VerifyCS.Diagnostic(ConfigureAwaitRequiredAnalyzer.Rule).WithSpan(8, 8, 8, 51),
                    },
                },
            }.WithoutGeneratedCodeVerification().WithConfigureAwaitAttributes().RunAsync();
        }

        [Test]
        public async Task NoWarn_For_Task_Yield()
        {
            string code = @"
[assembly:UseConfigureAwaitFalse()]

public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Yield();
    }
}
";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().WithConfigureAwaitAttributes().RunAsync();
        }

        [Test]
        public async Task Warn_For_Property()
        {
            string code = @"
[assembly:UseConfigureAwaitFalse()]

public class MyClass
{
    private static System.Threading.Tasks.Task MyTask => null;
    public static async System.Threading.Tasks.Task Foo()
    {
       await MyTask;
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
                        VerifyCS.Diagnostic(ConfigureAwaitRequiredAnalyzer.Rule).WithSpan(9, 8, 9, 20),
                    },
                },
            }.WithoutGeneratedCodeVerification().WithConfigureAwaitAttributes().RunAsync();
        }
    }
}
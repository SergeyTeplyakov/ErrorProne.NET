using ErrorProne.NET.AsyncAnalyzers;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.RemoveConfigureAwaitAnalyzer,
    ErrorProne.NET.AsyncAnalyzers.RemoveConfigureAwaitCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class RemoveConfigureAwaitCodeFixProviderTests
    {
        [Test]
        public async Task RemoveConfigureAwaitFalse()
        {
            string code = @"
[assembly:DoNotUseConfigureAwait()]
public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42).ConfigureAwait(false);
    }
}";

            string expected = @"
[assembly:DoNotUseConfigureAwait()]
public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42);
    }
}";

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(RemoveConfigureAwaitAnalyzer.Rule).WithSeverity(DiagnosticSeverity.Hidden).WithSpan(7, 52, 7, 73).WithMessage("bar"),
                    },
                },
                FixedState =
                {
                    Sources = { expected },
                },
            }.WithoutGeneratedCodeVerification().WithConfigureAwaitAttributes().RunAsync();
        }

        [Test]
        public async Task RemoveConfigureAwaitFalse_With_Right_Formatting()
        {
            string code = @"
[assembly:DoNotUseConfigureAwait()]
public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42)
         .ConfigureAwait(false);
    }
}";

            string expected = @"
[assembly:DoNotUseConfigureAwait()]
public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42);
    }
}";

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(RemoveConfigureAwaitAnalyzer.Rule).WithSeverity(DiagnosticSeverity.Hidden).WithSpan(8, 11, 8, 32).WithMessage("bar"),
                    },
                },
                FixedState =
                {
                    Sources = { expected },
                },
            }.WithoutGeneratedCodeVerification().WithConfigureAwaitAttributes().RunAsync();
        }
    }
}
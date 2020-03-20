using ErrorProne.NET.AsyncAnalyzers;
using ErrorProne.NET.TestHelpers;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
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
                        VerifyCS.Diagnostic(AddConfigureAwaitAnalyzer.Rule).WithSpan(7, 8, 7, 51),
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
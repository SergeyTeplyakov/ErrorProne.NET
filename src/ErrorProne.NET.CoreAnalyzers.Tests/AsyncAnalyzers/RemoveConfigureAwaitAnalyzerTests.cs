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

[System.AttributeUsage(System.AttributeTargets.Assembly)]
public class DoNotUseConfigureAwait : System.Attribute { }

public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42).[|ConfigureAwait(false)|];
    }
}
";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_For_Property()
        {
            string code = @"
[assembly:DoNotUseConfigureAwait()]

[System.AttributeUsage(System.AttributeTargets.Assembly)]
public class DoNotUseConfigureAwait : System.Attribute { }

public class MyClass
{
    private static System.Threading.Tasks.Task MyTask => null;
    public static async System.Threading.Tasks.Task Foo()
    {
       await MyTask.[|ConfigureAwait(false)|];
    }
}
";

            await VerifyCS.VerifyAsync(code);
        }
    }
}
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

[System.AttributeUsage(System.AttributeTargets.Assembly)]
public class UseConfigureAwaitFalseAttribute : System.Attribute { }

public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       [|await System.Threading.Tasks.Task.Delay(42)|];
    }
}
";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_For_Task_Yield()
        {
            string code = @"
[assembly:UseConfigureAwaitFalse()]

[System.AttributeUsage(System.AttributeTargets.Assembly)]
public class UseConfigureAwaitFalseAttribute : System.Attribute { }

public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Yield();
    }
}
";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_For_Property()
        {
            string code = @"
[assembly:UseConfigureAwaitFalse()]

[System.AttributeUsage(System.AttributeTargets.Assembly)]
public class UseConfigureAwaitFalseAttribute : System.Attribute { }

public class MyClass
{
    private static System.Threading.Tasks.Task MyTask => null;
    public static async System.Threading.Tasks.Task Foo()
    {
       [|await MyTask|];
    }
}
";
            await VerifyCS.VerifyAsync(code);
        }
    }
}
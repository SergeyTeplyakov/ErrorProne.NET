using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.ConfigureAwaitRequiredAnalyzer,
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

[System.AttributeUsage(System.AttributeTargets.Assembly)]
public class UseConfigureAwaitFalseAttribute : System.Attribute { }

public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       [|await System.Threading.Tasks.Task.Delay(42)|];
    }
}";

            string expected = @"
[assembly:UseConfigureAwaitFalse()]

[System.AttributeUsage(System.AttributeTargets.Assembly)]
public class UseConfigureAwaitFalseAttribute : System.Attribute { }

public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42).ConfigureAwait(false);
    }
}";
            await VerifyCS.VerifyCodeFixAsync(code, expected);
        }
    }
}
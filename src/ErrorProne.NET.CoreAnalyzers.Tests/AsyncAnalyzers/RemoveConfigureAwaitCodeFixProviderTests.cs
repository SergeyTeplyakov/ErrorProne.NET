using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.RedundantConfigureAwaitFalseAnalyzer,
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

[System.AttributeUsage(System.AttributeTargets.Assembly)]
public class DoNotUseConfigureAwait : System.Attribute { }

public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42).[|ConfigureAwait(false)|];
    }
}";

            string expected = @"
[assembly:DoNotUseConfigureAwait()]

[System.AttributeUsage(System.AttributeTargets.Assembly)]
public class DoNotUseConfigureAwait : System.Attribute { }

public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42);
    }
}";

            await VerifyCS.VerifyCodeFixAsync(code, expected);
        }

        [Test]
        public async Task RemoveConfigureAwaitFalse_With_Right_Formatting()
        {
            string code = @"
[assembly:DoNotUseConfigureAwait()]

[System.AttributeUsage(System.AttributeTargets.Assembly)]
public class DoNotUseConfigureAwait : System.Attribute { }

public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42)
         .[|ConfigureAwait(false)|];
    }
}";

            string expected = @"
[assembly:DoNotUseConfigureAwait()]

[System.AttributeUsage(System.AttributeTargets.Assembly)]
public class DoNotUseConfigureAwait : System.Attribute { }

public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42);
    }
}";

            await VerifyCS.VerifyCodeFixAsync(code, expected);
        }
    }
}
using ErrorProne.NET.AsyncAnalyzers;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class RemoveConfigureAwaitCodeFixProviderTests : CSharpCodeFixTestFixture<RemoveConfigureAwaitCodeFixProvider>
    {
        [Test]
        public void RemoveConfigureAwaitFalse()
        {
            string code = @"
[assembly:DoNotUseConfigureAwait()]
public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42).[|ConfigureAwait(false)|];
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

            TestCodeFix(code, expected, RemoveConfigureAwaitAnalyzer.Rule);
        }

        [Test]
        public void RemoveConfigureAwaitFalse_With_Right_Formatting()
        {
            string code = @"
[assembly:DoNotUseConfigureAwait()]
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
public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42);
    }
}";

            TestCodeFix(code, expected, RemoveConfigureAwaitAnalyzer.Rule);
        }
    }
}
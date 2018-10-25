using ErrorProne.NET.AsyncAnalyzers;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class AddConfigureAwaitCodeFixProviderTests : CSharpCodeFixTestFixture<AddConfigureAwaitCodeFixProvider>
    {
        [Test]
        public void AddConfigureAwaitFalse()
        {
            string code = @"
[assembly:UseConfigureAwaitFalse()]
public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       [|await System.Threading.Tasks.Task.Delay(42)|];
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

            TestCodeFix(code, expected, AddConfigureAwaitAnalyzer.Rule);
        }
    }
}
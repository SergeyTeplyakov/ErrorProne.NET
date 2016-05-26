using ErrorProne.NET.Rules.AsyncAnalyzers;
using ErrorProne.NET.Rules.ExceptionHandling;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.ExceptionHandling
{
    [TestFixture]
    public class RedundantAwaitCodeFixTests : CSharpCodeFixTestFixture<RedundantAwaitCodeFixProvider>
    {
        [Test]
        public void ConvertSingleAwait()
        {
            string code = @"
using System.Threading.Tasks;
class Test
{
    public async Task<int> [|Foo|](string s)
    {
        return await Task.FromResult(42);
    }
}";

            string expected = @"
using System.Threading.Tasks;
class Test
{
    public Task<int> Foo(string s)
    {
        return Task.FromResult(42);
    }
}";

            TestCodeFix(code, expected, RedundantAwaitAnalyzer.Rule);
        }

        [Test]
        public void ConvertMultipleAwaits()
        {
            string code = @"
using System.Threading.Tasks;
class Test
{
    public async Task<int> [|Foo|](string s)
    {
        if (s == null) return await Task.FromResult(1);
        return await Task.FromResult(42);
    }
}";

            string expected = @"
using System.Threading.Tasks;
class Test
{
    public Task<int> Foo(string s)
    {
        if (s == null) return Task.FromResult(1);
        return Task.FromResult(42);
    }
}";

            TestCodeFix(code, expected, RedundantAwaitAnalyzer.Rule);
        }

        [Test]
        public void ConvertTernaryReturn()
        {
            string code = @"
using System.Threading.Tasks;
class Test
{
    public async Task<int> [|Foo|](string s)
    {
        return s == null ? await Task.FromResult(1) : await Task.FromResult(42);
    }
}";

            string expected = @"
using System.Threading.Tasks;
class Test
{
    public Task<int> Foo(string s)
    {
        return s == null ? Task.FromResult(1) : Task.FromResult(42);
    }
}";

            TestCodeFix(code, expected, RedundantAwaitAnalyzer.Rule);
        }
    }
}
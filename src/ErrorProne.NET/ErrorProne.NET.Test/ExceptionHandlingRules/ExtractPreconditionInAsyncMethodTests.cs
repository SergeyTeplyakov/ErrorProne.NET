using System.Collections.Generic;
using ErrorProne.NET.Common;
using ErrorProne.NET.Rules.ExceptionHandling;
using ErrorProne.NET.Rules.OtherRules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.ExceptionHandling
{
    [TestFixture]
    public class ExtractPreconditionInAsyncMethodTests : CSharpCodeFixTestFixture<AsyncMethodPreconditionCodeFixProvider>
    {
        [Test]
        public void ConvertSingleIfThrow()
        {
            string code = @"
using System.Threading.Tasks;
class Test
{
    public async Task Foo(string s)
    {
        if (s == null) [|throw new System.ArgumentException()|];
        await Task.Delay(42);
    }

    private void JustAnotherMethod()
    {
    }
}";

            string expected = @"
using System.Threading.Tasks;
class Test
{
    public Task Foo(string s)
    {
        if (s == null) throw new System.ArgumentException();
        return DoFoo(s);
    }

    private async Task DoFoo(string s)
    {
        await Task.Delay(42);
    }

    private void JustAnotherMethod()
    {
    }
}";

            this.TestCodeFix(code, expected, AsyncMethodPreconditionsAnalyzer.Rule);
        }

        [Test]
        public void ConvertSingleTwoThrows()
        {
            string code = @"
using System.Threading.Tasks;
class Test
{
    public async Task Foo(string s)
    {
        if (s == null) [|throw new System.ArgumentException()|];
        if (s.Length == 0) throw new System.ArgumentException();
        await Task.Delay(42);
    }
}";

            string expected = @"
using System.Threading.Tasks;
class Test
{
    public Task Foo(string s)
    {
        if (s == null) throw new System.ArgumentException();
        if (s.Length == 0) throw new System.ArgumentException();
        return DoFoo(s);
    }

    private async Task DoFoo(string s)
    {
        await Task.Delay(42);
    }
}";

            this.TestCodeFix(code, expected, AsyncMethodPreconditionsAnalyzer.Rule);
        }

        [Test]
        public void ExtractOnlyFirstOne()
        {
            string code = @"
using System.Threading.Tasks;
class Test
{
    public async Task Foo(string s)
    {
        if (s == null) [|throw new System.ArgumentException()|];
        if (s.Length == 0) throw new System.Exception();
        await Task.Delay(42);
    }
}";

            string expected = @"
using System.Threading.Tasks;
class Test
{
    public Task Foo(string s)
    {
        if (s == null) throw new System.ArgumentException();
        return DoFoo(s);
    }

    private async Task DoFoo(string s)
    {
        if (s.Length == 0) throw new System.Exception();
        await Task.Delay(42);
    }
}";

            this.TestCodeFix(code, expected, AsyncMethodPreconditionsAnalyzer.Rule);
        }
    }
}
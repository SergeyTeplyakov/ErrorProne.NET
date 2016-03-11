using ErrorProne.NET.Rules.ExceptionHandling;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.ExceptionHandling
{
    [TestFixture]
    public class ExtractPreconditionInIteratorBlockTests : CSharpCodeFixTestFixture<IteratorBlockPreconditionCodeFixProvider>
    {
        [Test]
        public void ConvertSingleIfThrow()
        {
            string code = @"
using System.Collections.Generic;
class Test
{
    public IEnumerable<string> Foo(string s)
    {
        if (s == null) [|throw new System.ArgumentException()|];
        yield return ""1"";
        yield return ""2"";
    }
}";

            string expected = @"
using System.Collections.Generic;
class Test
{
    public IEnumerable<string> Foo(string s)
    {
        if (s == null) throw new System.ArgumentException();
        return DoFoo(s);
    }

    private IEnumerable<string> DoFoo(string s)
    {
        yield return ""1"";
        yield return ""2"";
    }
}";

            this.TestCodeFix(code, expected, IteratorBlockPreconditionsAnalyzer.Rule);
        }

        [Test]
        public void ConvertSingleTwoThrows()
        {
            string code = @"
using System.Collections.Generic;
class Test
{
    public IEnumerable<string> Foo(string s)
    {
        if (s == null) [|throw new System.ArgumentException()|];
        if (s.Length == 0) throw new System.ArgumentException();
        System.Console.WriteLine(42);
        yield return ""1"";
        yield return ""2"";
    }
}";

            string expected = @"
using System.Collections.Generic;
class Test
{
    public IEnumerable<string> Foo(string s)
    {
        if (s == null) throw new System.ArgumentException();
        if (s.Length == 0) throw new System.ArgumentException();
        return DoFoo(s);
    }

    private IEnumerable<string> DoFoo(string s)
    {
        System.Console.WriteLine(42);
        yield return ""1"";
        yield return ""2"";
    }
}";

            this.TestCodeFix(code, expected, IteratorBlockPreconditionsAnalyzer.Rule);
        }

        [Test]
        public void ExtractOnlyFirstOne()
        {
            string code = @"
using System.Collections.Generic;
class Test
{
    public IEnumerable<string> Foo(string s)
    {
        if (s == null) [|throw new System.ArgumentException()|];
        if (s.Length == 0) throw new System.Exception();
        yield break;
    }
}";

            string expected = @"
using System.Collections.Generic;
class Test
{
    public IEnumerable<string> Foo(string s)
    {
        if (s == null) throw new System.ArgumentException();
        return DoFoo(s);
    }

    private IEnumerable<string> DoFoo(string s)
    {
        if (s.Length == 0) throw new System.Exception();
        yield break;
    }
}";

            this.TestCodeFix(code, expected, IteratorBlockPreconditionsAnalyzer.Rule);
        }
    }
}
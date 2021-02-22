using NUnit.Framework;
using System.Threading.Tasks;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.TaskInstanceToStringConversionAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class TaskToStringConversionAnalyzerTests
    {
        [Test]
        public async Task InterpolatedStringConversionWithMethodCall()
        {
            var test = @"
using System.Threading.Tasks;
class Test {
    Task FooBar() => null;
    string T() => $""fb: {[|FooBar()|]}"";
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task InterpolatedStringConversionWithMethodCallAndLocal()
        {
            var test = @"
using System.Threading.Tasks;
class Test {
    Task FooBar() => null;
    string T()
    {
        var fb = FooBar();
        return $""fb: {[|fb|]}"";
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task InterpolatedStringConversionWithVariable()
        {
            var test = @"
using System.Threading.Tasks;
class Test {
    string T(Task t) => $""t: {[|t|]}"";
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task InterpolatedStringConversionInStringConcat()
        {
            var test = @"
using System.Threading.Tasks;
class Test {
    string T(Task t) => $""t"" + [|t|];
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task InterpolatedStringConversionInStringConcat2()
        {
            var test = @"
using System.Threading.Tasks;
class Test {
    void T(Task t) => Foo($""t"" + [|t|]);
    void Foo(string s) {}
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task InterpolatedStringConversionReturn()
        {
            var test = @"
using System.Threading.Tasks;
class Test {
    string T(Task t) => string.Format(""{0}"", [|t|]);
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarnOnConversionToObject()
        {
            var test = @"
using System.Threading.Tasks;
class Test {
    object T(Task t) => t;
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarnOnConversionToObjectMethodCall()
        {
            var test = @"
using System.Threading.Tasks;
class Test {
    void T(Task t) => Foo(t);
    void Foo(object o) {}
}
";
            await Verify.VerifyAsync(test);
        }
    }
}
using NUnit.Framework;
using System.Threading.Tasks;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.AsyncVoidMethodAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class AsyncVoidMethodAnalyzerTests
    {
        [Test]
        public async Task WarnOnAsyncVoidInstanceMethod()
        {
            var test = @"
using System;
class Test {
    async void [|Foo|]() { await System.Threading.Tasks.Task.Delay(1); }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnAsyncVoidStaticMethod()
        {
            var test = @"
using System;
class Test {
    static async void [|Bar|]() { await System.Threading.Tasks.Task.Delay(1); }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarningOnAsyncTaskMethod()
        {
            var test = @"
using System;
using System.Threading.Tasks;
class Test {
    async Task Foo() { await Task.Delay(1); }
    static async Task Bar() { await Task.Delay(1); }
}
";
            await Verify.VerifyAsync(test);
        }
    }
}

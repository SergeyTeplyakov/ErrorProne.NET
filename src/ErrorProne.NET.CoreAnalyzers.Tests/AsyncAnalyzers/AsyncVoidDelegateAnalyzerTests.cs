using NUnit.Framework;
using System.Threading.Tasks;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.AsyncVoidDelegateAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class AsyncVoidDelegateAnalyzerTests
    {
        [Test]
        public async Task WarnOnAsyncVoidLambda()
        {
            var test = @"
using System;
class Test {
    void F(Action action) { }
    void T() {
        F([|async () => { }|]);
        F([|async delegate() { }|]);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task ReportWarningOnAsyncVoidLambdaWithParenthesizedOneParameter()
        {
            var test = @"
using System;
class Test {
    void F(Action<object> action) { }
    void T() {
        F([|async (x) => { }|]);
        F([|async delegate(object x) { }|]);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task ReportWarningOnAsyncVoidLambdaWithOneParameter()
        {
            var test = @"
using System;
class Test {
    void F(Action<object> action) { }
    void T() {
        F([|async x => {}|]);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task ReportWarningOnAsyncVoidAnonymousDelegateWithOneParameter()
        {
            var test = @"
using System;
class Test {
    void F(Action<object> action) { }
    void T() {
        F([|async (object x) => { }|]);
        F([|async delegate(object x) { }|]);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task ReportWarningOnAsyncVoidLambdaSetToVariable()
        {
            var test = @"
using System;
class Test {
    void F() {
        Action action = [|async () => {}|];
        Action action2 = [|async delegate() {}|];
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task ReportWarningOnAsyncVoidLambdaWithOneParameterSetToVariable()
        {
            var test = @"
using System;
class Test {
    void F() {
        Action<object> action = [|async (x) => {}|];
        Action<object> action2 = [|async delegate(object x) {}|];
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task ReportWarningOnAsyncVoidLambdaBeingUsedAsEventHandler()
        {
            var test = @"
using System;
class Test {
    void F() {
        EventHandler action1 = [|async (sender, e) => {}|];
        EventHandler action2 = [|async delegate(object sender, EventArgs e) {}|];
        EventHandler<MyEventArgs> action3 = [|async (sender, e) => {}|];
        EventHandler<MyEventArgs> action4 = [|async delegate(object sender, MyEventArgs e) {}|];
    }
    class MyEventArgs : EventArgs {}
}
";
            await Verify.VerifyAsync(test);
        }
    }
}
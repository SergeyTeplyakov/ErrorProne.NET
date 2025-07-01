using System.Threading.Tasks;
using NUnit.Framework;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.DoNotBlockUnnecessarilyInAsyncMethodsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;


namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class DoNotBlockUnnecessarilyInAsyncMethodsAnalyzerTests
    {
        [Test]
        public async Task WarnOnResultInAsyncMethod()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    public async Task MyMethod()
    {
        var t = Task.FromResult(42);
        var result = [|t.Result|];
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task WarnOnWaitInAsyncMethod()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    public async Task MyMethod()
    {
        var t = Task.FromResult(42);
        [|t.Wait()|];
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task WarnOnGetAwaiterGetResultInAsyncMethod()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    public async Task MyMethod()
    {
        var t = Task.FromResult(42);
        var result = [|t.GetAwaiter().GetResult()|];
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarnInNonAsyncMethod()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    public Task MyMethod()
    {
        var t = Task.FromResult(42);
        var result = t.Result;
        return t;
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarnOnNonTask()
        {
            string code = @"
public class MyClass
{
    public class Foo { public int Result => 42; }
    public async System.Threading.Tasks.Task MyMethod()
    {
        var f = new Foo();
        var result = f.Result;
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task WarnOnResultInAsyncLambda()
        {
            string code = @"
using System;
using System.Threading.Tasks;
public class MyClass
{
    public void MyMethod()
    {
        Func<Task> asyncLambda = async () =>
        {
            var t = Task.FromResult(42);
            var result = [|t.Result|];
        };
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task WarnOnWaitInAsyncLambda()
        {
            string code = @"
using System;
using System.Threading.Tasks;
public class MyClass
{
    public void MyMethod()
    {
        Func<Task> asyncLambda = async () =>
        {
            var t = Task.FromResult(42);
            [|t.Wait()|];
        };
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task WarnOnGetAwaiterGetResultInAsyncLambda()
        {
            string code = @"
using System;
using System.Threading.Tasks;
public class MyClass
{
    public void MyMethod()
    {
        Func<Task<int>> asyncLambda = async () =>
        {
            var t = Task.FromResult(42);
            return [|t.GetAwaiter().GetResult()|];
        };
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task WarnOnResultInAsyncLocalMethod()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    public void MyMethod()
    {
        async Task LocalAsyncMethod()
        {
            var t = Task.FromResult(42);
            var result = [|t.Result|];
        }
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task WarnOnWaitInAsyncLocalMethod()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    public void MyMethod()
    {
        async Task LocalAsyncMethod()
        {
            var t = Task.FromResult(42);
            [|t.Wait()|];
        }
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task WarnOnGetAwaiterGetResultInAsyncLocalMethod()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    public void MyMethod()
    {
        async Task<int> LocalAsyncMethod()
        {
            var t = Task.FromResult(42);
            return [|t.GetAwaiter().GetResult()|];
        }
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarnInNonAsyncLambda()
        {
            string code = @"
using System;
using System.Threading.Tasks;
public class MyClass
{
    public void MyMethod()
    {
        Func<int> lambda = () =>
        {
            var t = Task.FromResult(42);
            return t.Result;
        };
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarnInNonAsyncLocalMethod()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    public void MyMethod()
    {
        int LocalMethod()
        {
            var t = Task.FromResult(42);
            return t.Result;
        }
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task WarnOnResultInNestedAsyncLambda()
        {
            string code = @"
using System;
using System.Threading.Tasks;
public class MyClass
{
    public async Task MyMethod()
    {
        Func<Task> asyncLambda = async () =>
        {
            Func<Task> nestedAsyncLambda = async () =>
            {
                var t = Task.FromResult(42);
                var result = [|t.Result|];
            };
            await nestedAsyncLambda();
        };
        await asyncLambda();
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task WarnOnResultInAsyncDelegate()
        {
            string code = @"
using System;
using System.Threading.Tasks;
public class MyClass
{
    public void MyMethod()
    {
        Func<Task> asyncDelegate = async delegate()
        {
            var t = Task.FromResult(42);
            var result = [|t.Result|];
        };
    }
}";
            await VerifyCS.VerifyAsync(code);
        }
    }
}

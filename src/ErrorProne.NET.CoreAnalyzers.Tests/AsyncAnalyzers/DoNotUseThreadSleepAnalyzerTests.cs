using NUnit.Framework;
using System.Threading.Tasks;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.DoNotUseThreadSleepAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

using T = System.Threading.Thread;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class DoNotUseThreadSleepAnalyzerTests
    {
        [Test]
        public async Task WarnOnThreadSleepInAsyncMethod()
        {
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using T = System.Threading.Thread;

class Test {
    async Task Foo() 
    { 
        [|Thread.Sleep(1000)|];
        await Task.Delay(1);
        [|T.Sleep(500)|];
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnThreadSleepInAsyncMethod_With_TimeSpan()
        {
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class Test {
    async Task Foo() 
    { 
        [|Thread.Sleep(TimeSpan.FromSeconds(1))|];
        await Task.Delay(1);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnThreadSleepInAsyncVoidMethod()
        {
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class Test {
    async void Bar() 
    { 
        [|Thread.Sleep(500)|];
        await Task.Delay(1);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnThreadSleepWithTimeSpanInAsyncMethod()
        {
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class Test {
    async Task<int> GetValue() 
    { 
        [|Thread.Sleep(TimeSpan.FromSeconds(1))|];
        await Task.Delay(1);
        return 42;
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarningOnThreadSleepInSyncMethod()
        {
            var test = @"
using System;
using System.Threading;

class Test {
    void SyncMethod() 
    { 
        Thread.Sleep(1000);
    }
    
    int GetValue()
    {
        Thread.Sleep(500);
        return 42;
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarningOnTaskDelayInAsyncMethod()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test {
    async Task Foo() 
    { 
        await Task.Delay(1000);
    }
    
    async Task<string> GetData()
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
        return ""data"";
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarningOnOtherMethodsWithSleepName()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class CustomClass
{
    public static void Sleep(int ms) { }
}

class Test {
    async Task Foo() 
    { 
        CustomClass.Sleep(1000);
        await Task.Delay(1);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnThreadSleepInAsyncLambda()
        {
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class Test {
    void TestAsyncLambda() 
    { 
        Func<Task> asyncLambda = async () =>
        {
            [|Thread.Sleep(1000)|];
            await Task.Delay(1);
        };
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnThreadSleepInAsyncLambdaWithParameter()
        {
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class Test {
    void TestAsyncLambdaWithParam() 
    { 
        Func<int, Task> asyncLambda = async (x) =>
        {
            [|Thread.Sleep(x * 100)|];
            await Task.Delay(x);
        };
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnThreadSleepInAsyncLocalFunction()
        {
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class Test {
    void TestAsyncLocalFunction() 
    { 
        async Task LocalAsyncMethod()
        {
            [|Thread.Sleep(500)|];
            await Task.Delay(1);
        }
        
        LocalAsyncMethod();
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnThreadSleepInAsyncLocalFunctionWithReturnValue()
        {
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class Test {
    void TestAsyncLocalFunctionWithReturn() 
    { 
        async Task<int> LocalAsyncMethod(int input)
        {
            [|Thread.Sleep(input)|];
            await Task.Delay(1);
            return input * 2;
        }
        
        var result = LocalAsyncMethod(100);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarningOnThreadSleepInSyncLambda()
        {
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class Test {
    void TestSyncLambda() 
    { 
        Action syncLambda = () =>
        {
            Thread.Sleep(1000);
        };
        
        Func<int, int> syncLambdaWithReturn = (x) =>
        {
            Thread.Sleep(x);
            return x * 2;
        };
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarningOnThreadSleepInSyncLocalFunction()
        {
            var test = @"
using System;
using System.Threading;

class Test {
    void TestSyncLocalFunction() 
    { 
        void LocalSyncMethod()
        {
            Thread.Sleep(500);
        }
        
        int LocalSyncMethodWithReturn(int input)
        {
            Thread.Sleep(input);
            return input * 2;
        }
        
        LocalSyncMethod();
        var result = LocalSyncMethodWithReturn(100);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnThreadSleepInNestedAsyncLambda()
        {
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class Test {
    async Task TestNestedAsyncLambda() 
    { 
        Func<Task> outerLambda = async () =>
        {
            Func<Task> innerLambda = async () =>
            {
                [|Thread.Sleep(200)|];
                await Task.Delay(1);
            };
            
            await innerLambda();
            [|Thread.Sleep(100)|];
            await Task.Delay(1);
        };
        
        await outerLambda();
    }
}
";
            await Verify.VerifyAsync(test);
        }
    }
}

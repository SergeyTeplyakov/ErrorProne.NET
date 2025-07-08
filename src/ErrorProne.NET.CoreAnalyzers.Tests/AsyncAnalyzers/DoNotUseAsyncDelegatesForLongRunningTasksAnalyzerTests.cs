using System;
using NUnit.Framework;
using System.Threading.Tasks;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.DoNotUseAsyncDelegatesForLongRunningTasksAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class DoNotUseAsyncDelegatesForLongRunningTasksAnalyzerTests
    {
        [Test]
        public async Task WarnOnAsyncLambdaWithLongRunning()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test 
{
    void TestMethod() 
    {
        [|Task.Factory.StartNew(async () => { await Task.Delay(100); }, TaskCreationOptions.LongRunning)|];
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnAsyncDelegateWithLongRunning()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test 
{
    void TestMethod() 
    {
        [|Task.Factory.StartNew(async delegate() { await Task.Delay(100); }, TaskCreationOptions.LongRunning)|];
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnAsyncMethodReferenceWithLongRunning()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test 
{
    async Task AsyncMethod() => await Task.Delay(100);
    
    void TestMethod() 
    {
        [|Task.Factory.StartNew(AsyncMethod, TaskCreationOptions.LongRunning)|];
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnAsyncLambdaWithCombinedOptions()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test 
{
    void TestMethod() 
    {
        [|Task.Factory.StartNew(async () => { await Task.Delay(100); }, TaskCreationOptions.LongRunning | TaskCreationOptions.AttachedToParent)|];
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarningOnSyncLambdaWithLongRunning()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test 
{
    void TestMethod() 
    {
        Task.Factory.StartNew(() => { System.Threading.Thread.Sleep(100); }, TaskCreationOptions.LongRunning);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarningOnSyncDelegateWithLongRunning()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test 
{
    void TestMethod() 
    {
        Task.Factory.StartNew(delegate() { System.Threading.Thread.Sleep(100); }, TaskCreationOptions.LongRunning);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarningOnAsyncLambdaWithoutLongRunning()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test 
{
    void TestMethod() 
    {
        Task.Factory.StartNew(async () => { await Task.Delay(100); });
        Task.Factory.StartNew(async () => { await Task.Delay(100); }, TaskCreationOptions.None);
        Task.Factory.StartNew(async () => { await Task.Delay(100); }, TaskCreationOptions.AttachedToParent);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarningOnTaskRun()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test 
{
    void TestMethod() 
    {
        Task.Run(async () => { await Task.Delay(100); });
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnTaskFactoryStartNewWithDirectOptions()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test 
{
    void TestMethod() 
    {
        var factory = Task.Factory;
        [|factory.StartNew(async () => { await Task.Delay(100); }, TaskCreationOptions.LongRunning)|];
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnNewTaskFactoryStartNew()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test 
{
    void TestMethod() 
    {
        var factory = new TaskFactory();
        [|factory.StartNew(async () => { await Task.Delay(100); }, TaskCreationOptions.LongRunning)|];
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarningOnSyncMethodReferenceWithLongRunning()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test 
{
    void SyncMethod() => System.Threading.Thread.Sleep(100);
    
    void TestMethod() 
    {
        Task.Factory.StartNew(SyncMethod, TaskCreationOptions.LongRunning);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnAsyncLambdaWithParametersAndLongRunning()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test 
{
    void TestMethod() 
    {
        var state = ""test"";
        [|Task.Factory.StartNew(async (obj) => { await Task.Delay(100); }, state, TaskCreationOptions.LongRunning)|];
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarningOnGenericTaskFactorySyncMethodReferenceWithLongRunning()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test 
{
    int SyncMethod() 
    { 
        System.Threading.Thread.Sleep(100); 
        return 42; 
    }
    
    void TestMethod() 
    {
        var factory = new TaskFactory<int>();
        factory.StartNew(SyncMethod, TaskCreationOptions.LongRunning);
    }
}
";
            await Verify.VerifyAsync(test);
        }
    }
}

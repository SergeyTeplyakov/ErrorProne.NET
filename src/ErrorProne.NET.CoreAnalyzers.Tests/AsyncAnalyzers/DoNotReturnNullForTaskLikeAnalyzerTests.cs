using System.Threading.Tasks;
using NUnit.Framework;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.DoNotReturnNullForTaskLikeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class DoNotReturnNullForTaskLikeAnalyzerTests
    {
        [Test]
        public async Task Warn_When_Null_Is_Returned_For_Task()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    public Task Foo()
    {
        [|return null;|]
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_When_Null_Is_Returned_For_Task_Of_T()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    public Task<int> Foo()
    {
        [|return null;|]
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_When_Null_Is_Part_Of_Switch()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    Task FooBar(int x) => [|x switch { 1 => null, _ => Task.CompletedTask }|];
}";
            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task Warn_When_Null_Is_Returned_For_Task_Of_T_Expression_Body()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    Task FooBar(int x) => [|x switch { 1 => null, _ => Task.CompletedTask }|];
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_When_Null_Is_Returned_For_Task_In_Local_Function()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    public void Foo()
    {
        Task Bar(bool f)
        {
            if (f)
                [|return null;|]

            return Task.CompletedTask;  
        }
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_When_Null_Is_Returned_For_Task_In_Lambda()
        {
            string code = @"
using System;
using System.Threading.Tasks;
public class MyClass
{
    public void Foo()
    {
        Func<Task> f = () => { [|return null;|] };
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task No_Warn_For_Return_In_Async_Task()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    public async Task Foo()
    {
        await Task.Yield();
        return;
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task No_Warn_When_Nullability_Is_On()
        {
            string code = @"
using System.Threading.Tasks;
#nullable enable
public class MyClass
{
    public Task Foo()
    {
        return null;
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task No_Warn_When_Default_Is_Returned_For_ValueTask()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    public ValueTask Foo()
    {
        return default;
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task No_Warn_When_Default_Is_Returned_For_ValueTask_Of_T()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    public ValueTask<int> Foo()
    {
        return default;
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_When_Default_Is_Returned_For_Task()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    public Task Foo()
    {
        [|return default;|]
    }
}";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_When_Default_Is_Returned_For_Task_Of_T()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    public Task<int> Foo()
    {
        [|return default;|]
    }
}";
            await VerifyCS.VerifyAsync(code);
        }
    }
}

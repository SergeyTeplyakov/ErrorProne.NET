using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.TaskInUsingBlockAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class TaskInUsingBlockAnalyzerTests
    {
        [Test]
        public async Task Warn_On_Using_Task_In_Using_Block()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    public static async Task Using_Task_In_Using()
    {
        [|using var _ = GetTask();|]
        var t = GetTask();
        static Task GetTask() => Task.CompletedTask;
    }
}
";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_On_Using_Task_In_Using_After_Await()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    public static async Task Using_Task_In_Using()
    {
        [|using var _ = await GetTask();|]
        var t = GetTask();
        static Task<Task> GetTask() => Task.Run<Task>(() => Task.CompletedTask);
    }
}
";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_On_Using_Task_In_Using_Statement()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    public static async Task Using_Task_In_Using_Statement()
    {
        [|using (var _ = GetTask())
        {

        }|]
        static Task GetTask() => Task.CompletedTask;
    }
}
";
            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task Warn_On_Using_Task_In_Using_Statement2()
        {
            string code = @"
using System.Threading.Tasks;
public class MyClass
{
    public static async Task Using_Task_In_Using_Statement()
    {
        [|using (GetTask())
        {

        }|]
        static Task GetTask() => Task.CompletedTask;
    }
}
";
            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task Warn_On_Using_Task_In_Using_Statement_InFunc()
        {
            string code = @"
using System.Threading.Tasks;
using System;
public class MyClass
{
    public static async Task Using_Task_In_Using_Statement(Func<Task> getTask)
    {
        [|using (getTask())
        {
            var t = getTask();
        }|]
    }
}
";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task No_Warn_On_Using_Task_Inside_Other_Using()
        {
            string code = @"
using System.Threading.Tasks;
using System;
public class MyClass : IDisposable
{
    public void Dispose() { }

    public static async Task Using_Task_In_Using_Statement(Func<Task> getTask)
    {
        using (var m = new MyClass())
        {
            await getTask();
            var t = getTask();
        }
    }
}
";
            await VerifyCS.VerifyAsync(code);
        }
    }
}
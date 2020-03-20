using ErrorProne.NET.TestHelpers;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.UnobservedResultAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class UnobservedResultAnalyzerTests
    {
        [Test]
        public async Task Method_That_Returns_Exception_Should_Be_Observed()
        {
            string code = @"
class FooBar
{
    public static System.Exception Foo() => null;

    public static void Test()
    {
        [|Foo|]();
    }
}
";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task Method_That_Returns_Result_Should_Be_Observed()
        {
            string code = @"
class Result {}

class FooBar
{
    public static Result Foo() => null;

    public static void Test()
    {
        [|Foo|]();
    }
}
";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task Result_That_Flows_Through_Extension_Method_Is_Observed()
        {
            string code = @"
class Result {}
static class ResultEx {
    public static Result Handle(this Result r) => r;
}
class FooBar
{
    public static Result Foo() => null;

    public static void Test()
    {
        Foo().Handle();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task Result_That_Flows_Through_Extension_Method_Is_Observed2()
        {
            string code = @"
class Result {}
static class ResultEx {
    public static Result Handle(this Result r) => r;
}
class FooBar
{
    public static Result Foo => null;

    public static void Test()
    {
        Foo.Handle();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task Result_That_Flows_Through_Extension_Method_Is_Observed_For_Tasks()
        {
            string code = @"
class Result {}
static class ResultEx {
    public static System.Threading.Tasks.Task<Result> Handle(this System.Threading.Tasks.Task<Result> r) => r;
}
class FooBar
{
    public static async System.Threading.Tasks.Task<Result> Foo() => null;

    public static async System.Threading.Tasks.Task Test()
    {
        await Foo().Handle();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task Method_That_Returns_Result_Should_Be_Observed_In_Await()
        {
            string code = @"
class Result {}

class FooBar
{
    public static System.Threading.Tasks.Task<Result> Foo() => null;

    public static async System.Threading.Tasks.Task Test()
    {
        [|await Foo()|];
    }
}
";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task Method_That_Returns_Result_Should_Be_Observed_In_Await_With_ConfigureAwait()
        {
            string code = @"
class Result {}

class FooBar
{
    public static System.Threading.Tasks.Task<Result> Foo() => null;

    public static async System.Threading.Tasks.Task Test()
    {
        [|await Foo().ConfigureAwait(false)|];
    }
}
";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task Warn_On_Awaiting_The_Task_That_Returns_Result()
        {
            string code = @"
class Result {}

class FooBar
{
    public static System.Threading.Tasks.Task<Result> Foo => null;

    public static async System.Threading.Tasks.Task Test()
    {
        [|await Foo.ConfigureAwait(false)|];
    }
}
";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task Warn_On_Awaiting_The_Task_That_Returns_Result_Without_ConfigureAwait()
        {
            string code = @"
class Result {}

class FooBar
{
    public static System.Threading.Tasks.Task<Result> Foo => null;

    public static async System.Threading.Tasks.Task Test()
    {
        [|await Foo|];
    }
}
";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task Method_That_Returns_ResultBase_Should_Be_Observed()
        {
            string code = @"
class ResultBase {}
class MyValue : ResultBase {}

class FooBar
{
    public static MyValue Foo() => null;

    public static void Test()
    {
        [|Foo|]();
    }
}
";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task Method_That_Returns_Possible_Should_Be_Observed()
        {
            string code = @"
struct Possible<T> {}

class FooBar
{
    public static Possible<int> Foo() => default;

    public static void Test()
    {
        [|Foo|]();
    }
}
";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task NoWarnings_For_Tasks_ContinueWith()
        {
            string code = @"
class FooBar
{
    public static void Test(System.Threading.Tasks.Task task)
    {
        task.ContinueWith(t => {});
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task NoWarnings_For_Methods_Starts_With_Throw()
        {
            string code = @"
class FooBar
{
    private static System.Exception Throw() {throw new System.Exception();}
    public static void Test()
    {
        Throw();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task NoWarnings_For_Methods_Starts_With_ThrowAsync()
        {
            string code = @"
using System.Threading.Tasks;
class FooBar
{
    private static Task<T> ThrowAsync<T>() where T : System.Exception, new() {throw new T();}
    public static async Task Test()
    {
        await ThrowAsync<System.Exception>();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task NoWarnings_When_Exception_Type_Is_Infered()
        {
            string code = @"
class FooBar
{
    private static T Cast<T>(object o) where T : class => o as T;
    public static void Test()
    {
        object o = null;
        Cast<System.Exception>(o);
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }
    }
}
using System;
using NUnit.Framework;
using System.Threading.Tasks;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.DoNotValidateArgumentsInAsyncMethodsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class DoNotValidateArgumentsInAsyncMethodsAnalyzerTests
    {
        [Test]
        public async Task WarnOnArgumentNullExceptionInPublicAsyncMethod()
        {
            var test = @"
using System;
using System.Threading.Tasks;

public class Test 
{
    public async Task FooAsync(string s)
    { 
        if (s == null) [|throw new ArgumentNullException(nameof(s));|]
        await Task.Delay(42);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnArgumentExceptionInPublicAsyncMethod()
        {
            var test = @"
using System;
using System.Threading.Tasks;

public class Test 
{
    public async Task FooAsync(string s)
    { 
        if (string.IsNullOrEmpty(s)) [|throw new ArgumentException(""Value cannot be empty"", nameof(s));|]
        await Task.Delay(42);
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task WarnOnArgumentException_Via_Factory_Method_InPublicAsyncMethod()
        {
            var test = @"
using System;
using System.Threading.Tasks;

public class Test 
{
    public async Task FooAsync(string s)
    { 
        if (string.IsNullOrEmpty(s)) [|throw CreateArgumentException(nameof(s));|]
        await Task.Delay(42);
    }

    static ArgumentException CreateArgumentException(string paramName)
    {
        return new ArgumentException(""Value cannot be empty"", paramName);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnArgumentOutOfRangeExceptionInPublicAsyncMethod()
        {
            var test = @"
using System;
using System.Threading.Tasks;

public class Test 
{
    public async Task FooAsync(int value)
    { 
        if (value < 0) [|throw new ArgumentOutOfRangeException(nameof(value));|]
        await Task.Delay(42);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnArgumentNullExceptionThrowIfNull()
        {
            var test = @"
using System;
using System.Threading.Tasks;

public class Test 
{
    public async Task FooAsync(string s)
    { 
        [|ArgumentNullException.ThrowIfNull(s)|];
        await Task.Delay(42);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task DoNotWarnOnNonPublicAsyncMethod()
        {
            var test = @"
using System;
using System.Threading.Tasks;

public class Test 
{
    private async Task FooAsync(string s)
    { 
        if (s == null) throw new ArgumentNullException(nameof(s));
        await Task.Delay(42);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task DoNotWarnOnAsyncMethodInNonPublicClass()
        {
            var test = @"
using System;
using System.Threading.Tasks;

internal class Test 
{
    public async Task FooAsync(string s)
    { 
        if (s == null) throw new ArgumentNullException(nameof(s));
        await Task.Delay(42);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task DoNotWarnOnSyncMethod()
        {
            var test = @"
using System;
using System.Threading.Tasks;

public class Test 
{
    public void Foo(string s)
    { 
        if (s == null) throw new ArgumentNullException(nameof(s));
        // Some synchronous work
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task DoNotWarnOnThrowAfterAwait()
        {
            var test = @"
using System;
using System.Threading.Tasks;

public class Test 
{
    public async Task FooAsync(string s)
    { 
        await Task.Delay(42);
        if (s == null) throw new ArgumentNullException(nameof(s)); // This is fine, not argument validation
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task DoNotWarnOnNonArgumentException()
        {
            var test = @"
using System;
using System.Threading.Tasks;

public class Test 
{
    public async Task FooAsync(string s)
    { 
        if (s == null) throw new InvalidOperationException();
        await Task.Delay(42);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnCustomArgumentException()
        {
            var test = @"
using System;
using System.Threading.Tasks;

public class CustomArgumentException : ArgumentException
{
    public CustomArgumentException(string message) : base(message) { }
}

public class Test 
{
    public async Task FooAsync(string s)
    { 
        if (s == null) [|throw new CustomArgumentException(""Invalid argument"");|]
        await Task.Delay(42);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task DoNotWarnOnThrowInLambda()
        {
            var test = @"
using System;
using System.Linq;
using System.Threading.Tasks;

public class Test 
{
    public async Task FooAsync(string[] items)
    { 
        var result = items.Where(x => 
        {
            if (x == null) throw new ArgumentNullException(nameof(x)); // This is in a lambda, not direct validation
            return x.Length > 0;
        });
        await Task.Delay(42);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task DoNotWarnOnThrowInLocalFunction()
        {
            var test = @"
using System;
using System.Threading.Tasks;

public class Test 
{
    public async Task FooAsync(string s)
    { 
        void ValidateArgument(string arg)
        {
            if (arg == null) throw new ArgumentNullException(nameof(arg)); // This is in a local function
        }
        
        ValidateArgument(s);
        await Task.Delay(42);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnGenericAsyncMethod()
        {
            var test = @"
using System;
using System.Threading.Tasks;

public class Test 
{
    public async Task<T> FooAsync<T>(T value) where T : class
    { 
        if (value == null) [|throw new ArgumentNullException(nameof(value));|]
        await Task.Delay(42);
        return value;
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnAsyncMethodReturningCustomTask()
        {
            var test = @"
using System;
using System.Threading.Tasks;

public class Test 
{
    public async ValueTask FooAsync(string s)
    { 
        if (s == null) [|throw new ArgumentNullException(nameof(s));|]
        await Task.Delay(42);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnMultipleValidationStatements()
        {
            var test = @"
using System;
using System.Threading.Tasks;

public class Test 
{
    public async Task FooAsync(string s, int value)
    { 
        if (s == null) [|throw new ArgumentNullException(nameof(s));|]
        if (value < 0) [|throw new ArgumentOutOfRangeException(nameof(value));|]
        await Task.Delay(42);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnOnNestedPublicClass()
        {
            var test = @"
using System;
using System.Threading.Tasks;

public class Outer 
{
    public class Inner
    {
        public async Task FooAsync(string s)
        { 
            if (s == null) [|throw new ArgumentNullException(nameof(s));|]
            await Task.Delay(42);
        }
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task DoNotWarnOnNestedClassWithNonPublicParent()
        {
            var test = @"
using System;
using System.Threading.Tasks;

internal class Outer 
{
    public class Inner
    {
        public async Task FooAsync(string s)
        { 
            if (s == null) throw new ArgumentNullException(nameof(s));
            await Task.Delay(42);
        }
    }
}
";
            await Verify.VerifyAsync(test);
        }
    }
}

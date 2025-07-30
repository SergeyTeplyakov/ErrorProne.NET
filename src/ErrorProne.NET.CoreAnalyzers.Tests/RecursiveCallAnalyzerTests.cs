using NUnit.Framework;
using System.Threading.Tasks;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.RecursiveCallAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class RecursiveCallAnalyzerTests
    {
        [Test]
        public async Task WarnsOnUnconditionalRecursiveCall()
        {
            var test = @"
class C {
    void Foo() {
        [|Foo()|];
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_On_Different_Instance()
        {
            var test = @"
public class Node
{
    public void Foo() { Parent?.Foo();}
    public Node Parent { get; set; }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_With_Ref_Parameter_When_Touched()
        {
            var test = @"
public class Node
{
    public void Foo(ref int x)
    {
        Bar(ref x);
        Foo(ref x);
    }

    private void Bar(ref int x)
    {
        x++;
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_With_Ref_Parameter_When_Changed()
        {
            var test = @"
public class Node
{
    public void Foo(ref int x)
    {
        x = 42;
        Foo(ref x);
    }

    private void Bar(ref int x)
    {
        x++;
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task Warn_With_Ref_Parameter_When_Not_Touched()
        {
            var test = @"
public class Node
{
    public void Foo(ref int x)
    {
        [|Foo(ref x)|];
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnsOnConditionalRecursiveCall()
        {
            var test = @"
class C {
    void Foo(bool b) {
        if (b) [|Foo(b)|];
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task WarnsOnConditionalRecursiveCall_With_Named_Parameters()
        {
            var test = @"
class C {
    void Foo(bool b) {
        if (b) [|Foo(b: b)|];
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_When_Different_Argument_Is_Passed()
        {
            var test = @"
class C {
    void Foo(bool b) {
        if (b) Foo(false);
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_For_Factorial()
        {
            var test = @"
class C {
    int Factorial(int n)
    {
        if (n <= 1)
            return 1; // Base case
        return n * Factorial(n - 1); // Recursive call with changing argument
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarningOnNonRecursiveCall()
        {
            // The call is recursive, but we're not doing cross-procedural analysis.
            var test = @"
class C {
    void Foo() { Bar(); }
    void Bar() { Foo(); }
}
";
            await Verify.VerifyAsync(test);
        }
    }
}

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
        public async Task Warns_OnInvariantGuardedRecursiveCall()
        {
            // The guard 'b' is passed unchanged to the call, so once the branch is taken it is
            // taken forever -> provably infinite recursion.
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
        public async Task Warns_OnInvariantGuardedRecursiveCall_With_Named_Parameters()
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
        public async Task Warns_OnInvariantComparisonGuardedRecursiveCall()
        {
            var test = @"
class C {
    void Foo(int n) {
        if (n > 0) [|Foo(n)|];
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task Warns_When_Call_Is_Inside_Ternary_With_Invariant_Condition()
        {
            var test = @"
class C {
    int Foo(bool b) {
        return b ? [|Foo(b)|] : 0;
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task Warns_When_Call_Is_Inside_While_With_Invariant_Condition()
        {
            var test = @"
class C {
    void Foo(int n) {
        while (n > 0) {
            [|Foo(n)|];
        }
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_When_Guard_Parameter_Is_Mutated()
        {
            // 'n' is decremented before the recursive call, so the guard is not invariant.
            var test = @"
class C {
    void Foo(int n) {
        if (n > 0) {
            n--;
            Foo(n);
        }
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_When_Guard_Parameter_Is_Mutated_Via_Compound_Assignment()
        {
            var test = @"
class C {
    void Foo(int n) {
        if (n > 0) {
            n += 1;
            Foo(n);
        }
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_When_Guard_Parameter_Is_Mutated_Via_Deconstruction()
        {
            var test = @"
class C {
    void Foo(int n) {
        if (n > 0) {
            (n, var x) = (n - 1, 0);
            Foo(n);
        }
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task Warns_When_Call_Is_In_Try_Body()
        {
            // A try body executes unconditionally, so unconditional recursion in it is still a bug.
            var test = @"
class C {
    void Foo() {
        try {
            [|Foo()|];
        } catch { }
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_When_Call_Is_In_Catch()
        {
            var test = @"
using System;
class C {
    void Foo() {
        try { } catch (Exception) { Foo(); }
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_When_Call_Is_In_Finally()
        {
            var test = @"
class C {
    void Foo() {
        try { } finally { Foo(); }
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_When_Guard_Depends_On_Instance_Field()
        {
            var test = @"
class C {
    private bool _flag;
    void Foo() {
        if (_flag) Foo();
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_When_Guard_Depends_On_Property()
        {
            var test = @"
class C {
    private bool Flag { get; set; }
    void Foo() {
        if (Flag) Foo();
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_When_Conditional_Early_Return_Terminates()
        {
            // Issue: 'if (n > 10) return;' can terminate the recursion before the call.
            var test = @"
class C {
    void Foo(int n) {
        n++;
        if (n > 10) return;
        Foo(n);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_When_Conditional_Throw_Terminates()
        {
            var test = @"
using System;
class C {
    void Foo(int n) {
        if (n > 10) throw new InvalidOperationException();
        Foo(n);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_When_Termination_Depends_On_Instance_State()
        {
            var test = @"
class C {
    private bool _done;
    void Foo() {
        if (_done) return;
        Foo();
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_When_Call_Is_Inside_Switch()
        {
            // We don't attempt to prove invariance through switch statements, so we stay safe and
            // do not warn.
            var test = @"
class C {
    void Foo(int n) {
        switch (n) {
            case 0: Foo(n); break;
        }
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task Warns_When_Unconditional_After_StraightLine_Statements()
        {
            var test = @"
using System;
class C {
    void Foo(int n) {
        Console.WriteLine(n);
        [|Foo(n)|];
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
        public async Task NoWarn_When_Same_Method_Is_Called_From_Lambda()
        {
            // Issue #318: a call to the same method from within a lambda is not
            // an unconditional recursive call -- the lambda body is deferred.
            var test = @"
using System;
class C {
    void Foo() {
        Action a = () => Foo();
        a();
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_When_Same_Method_Is_Called_From_AnonymousMethod()
        {
            var test = @"
using System;
class C {
    void Foo() {
        Action a = delegate { Foo(); };
        a();
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_When_Same_Method_Is_Called_From_LocalFunction()
        {
            var test = @"
class C {
    void Foo() {
        void Local() { Foo(); }
        Local();
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

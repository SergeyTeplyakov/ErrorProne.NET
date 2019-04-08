using ErrorProne.NET.TestHelpers;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.StructAnalyzers.UseInModifierForReadOnlyStructAnalyzer,
    ErrorProne.NET.StructAnalyzers.UseInModifierForReadOnlyStructCodeFixProvider>;

namespace ErrorProne.NET.StructAnalyzers.Tests
{
    [TestFixture]
    public class UseInModifierForReadOnlyStructCodeFixProviderTests
    {
        [Test]
        public async Task AddInModifier()
        {
            string code = @"readonly struct FooBar { public static void Foo([|FooBar fb|]) {} readonly (long, long, long) data; }";

            string expected = @"readonly struct FooBar { public static void Foo(in FooBar fb) {} readonly (long, long, long) data; }";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expected } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task AddInModifierToCallSites()
        {
            string code = @"
readonly struct FooBar
{
    public static void Foo([|FooBar fb|]) {Foo(fb);}
    readonly (long, long, long) data;
}";

            string expected = @"
readonly struct FooBar
{
    public static void Foo(in FooBar fb) {Foo(in fb);}
    readonly (long, long, long) data;
}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expected } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task AddInModifierToImplementingMethods()
        {
            string code = @"
readonly struct FooBar
{
    readonly (long, long, long) data;
}

interface IInterface
{
    void Method([|FooBar fb|]);
}

class Class
{
    public void Method([|FooBar fb|]) => throw null;
}
";

            string expected = @"
readonly struct FooBar
{
    readonly (long, long, long) data;
}

interface IInterface
{
    void Method(in FooBar fb);
}

class Class
{
    public void Method(in FooBar fb) => throw null;
}
";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expected } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task AddInModifierToExplicitImplementingMethods()
        {
            string code = @"
readonly struct FooBar
{
    readonly (long, long, long) data;
}

interface IInterface
{
    void Method([|FooBar fb|]);
}

class Class : IInterface
{
    void IInterface.Method(FooBar fb) => throw null;
}
";

            string expected = @"
readonly struct FooBar
{
    readonly (long, long, long) data;
}

interface IInterface
{
    void Method(in FooBar fb);
}

class Class : IInterface
{
    void IInterface.Method(in FooBar fb) => throw null;
}
";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expected } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task AddInModifierToOverridingMethods()
        {
            string code = @"
readonly struct FooBar
{
    readonly (long, long, long) data;
}

abstract class BaseClass
{
    protected abstract void Method([|FooBar fb|]);
}

class DerivedClass : BaseClass
{
    protected override void Method(FooBar fb) => throw null;
}
";

            string expected = @"
readonly struct FooBar
{
    readonly (long, long, long) data;
}

abstract class BaseClass
{
    protected abstract void Method(in FooBar fb);
}

class DerivedClass : BaseClass
{
    protected override void Method(in FooBar fb) => throw null;
}
";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expected } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task AddInModifierToCallSites2()
        {
            string code1 = @"
readonly struct FooBar
{
    public static void Foo([|FooBar fb|]) { }
    readonly (long, long, long) data;
}";
            string code2 = @"
class Referencer
{
    FooBar _value;
    public void Method()
    {
        FooBar.Foo(_value);
    }
}";

            string expected1 = @"
readonly struct FooBar
{
    public static void Foo(in FooBar fb) { }
    readonly (long, long, long) data;
}";
            string expected2 = @"
class Referencer
{
    FooBar _value;
    public void Method()
    {
        FooBar.Foo(in _value);
    }
}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code1, code2 } },
                FixedState = { Sources = { expected1, expected2 } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task AddInModifierWithTrivia()
        {
            string code = @"readonly struct FooBar
{
    public static void Foo(
        [|FooBar fb|])
    {
    }

    readonly (long, long, long) data;
}";

            string expected = @"readonly struct FooBar
{
    public static void Foo(
        in FooBar fb)
    {
    }

    readonly (long, long, long) data;
}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expected } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task NoCodeFixWhenUsedAsOut()
        {
            string code = @"
public readonly struct RS
{
    public static void UsedAsIn([|RS rs|])
    {
        UseAsOut(out rs);
    }

    public static void UseAsOut(out RS rs) => rs = default;

    readonly (long, long, long) data;
}";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task NoCodeFixWhenUsedAsRef()
        {
            string code = @"
public readonly struct RS
{
    public static void UsedAsIn([|RS rs|])
    {
        UseAsOut(ref rs);
    }

    public static void UseAsOut(ref RS rs) => rs = default;

    readonly (long, long, long) data;
}";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [TestCaseSource(nameof(GetDoNothingCodeFixes))]
        public async Task DoNothingCodeFixes(string code)
        {
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        public static IEnumerable<string> GetDoNothingCodeFixes()
        {
            // Captured in indexer
            yield return @"readonly struct FooBar { readonly (long, long, long) data; }
        class FooClass { public System.Func<FooBar> this[FooBar [|fb|]] => () => fb; }";

            // Captured in anonymous delegate
            yield return @"readonly struct FooBar { public static void Foo([|FooBar fb|]) { System.Func<FooBar> a = delegate(){ return fb;}; } readonly (long, long, long) data; }";

            // Captured in lambda
            yield return @"readonly struct FooBar { public static void Foo([|FooBar fb|]) {System.Func<FooBar> a = () => fb; } readonly (long, long, long) data; }";

            // Captured in lambda2
            yield return @"readonly struct FooBar { public static System.Func<FooBar> Foo([|FooBar fb|]) => () => fb; readonly (long, long, long) data; }";
        }
    }
}
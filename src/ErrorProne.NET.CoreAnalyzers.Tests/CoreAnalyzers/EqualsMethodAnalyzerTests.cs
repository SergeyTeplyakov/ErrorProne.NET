using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.SuspiciousEqualsMethodAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class EqualsMethodAnalyzerTests
    {
        [Test]
        public async Task Warn_For_Strongly_Typed_Equalsl()
        {
            string code = @"
public class MyS : System.IEquatable<MyS>
{
    public int Line { get; }
    public bool Equals(MyS other) => other != null;
}
";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(SuspiciousEqualsMethodAnalyzer.InstanceMembersAreNotUsedRule).WithSpan(5, 17, 5, 23),
                    },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task NoWarn_When_For_Pattern_Matching()
        {
            string code = @"
public readonly struct MyS : System.IEquatable<MyS>
{
    public int Line { get; }

    public override int GetHashCode() => 42;
    public override bool Equals(object other) => other is MyS s && Equals(s);
    public bool Equals(MyS s) => Line == s.Line;
    
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task NoWarn_When_For_Pattern_As()
        {
            string code = @"
public class MyS
{
    public int Line { get; }

    public override int GetHashCode() => 42;
    public override bool Equals(object other) => Equals(other as MyS);
    public bool Equals(MyS s) => Line == s.Line;
    
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task NoWarn_When_For_Is_Plus_Cast()
        {
            string code = @"
public readonly struct MyS : System.IEquatable<MyS>
{
    public int Line { get; }

    public override int GetHashCode() => 42;
    public override bool Equals(object other) => other is MyS && Equals((MyS)other);
    public bool Equals(MyS other) => Line == other.Line;
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task Warn_When_Only_Static_Members_Are_Used()
        {
            string code = @"
class Foo {public int X = 42;}
class FooBar
{
    private int _n = 42;
    private static int _s = 4;
    private static Foo _f = new Foo();

    public override bool Equals(object obj)
    {
        return _f.X == _s && 
            System.Collections.Generic.EqualityComparer<FooBar>.Default.Equals(obj as FooBar);
    }
}
";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(SuspiciousEqualsMethodAnalyzer.InstanceMembersAreNotUsedRule).WithSpan(9, 26, 9, 32),
                    },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task Warn_When_Only_Static_Members_Are_Used_In_Expression_Body_Impl()
        {
            string code = @"
class Foo {public int X = 42;}
class FooBar
{
    private int _n = 42;
    private static int _s = 4;
    private static Foo _f = new Foo();

    public override bool Equals(object obj)
       =>
        _f.X == _s && 
            System.Collections.Generic.EqualityComparer<FooBar>.Default.Equals(obj as FooBar);
}
";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(SuspiciousEqualsMethodAnalyzer.InstanceMembersAreNotUsedRule).WithSpan(9, 26, 9, 32),
                    },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task Warn_When_Only_Static_Members_Are_Used_With_Instance_Props()
        {
            string code = @"
class FooBar
{
    private int N => 42;

    public override bool Equals(object obj)
    {
        return System.Collections.Generic.EqualityComparer<FooBar>.Default.Equals(obj as FooBar);
    }
}
";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(SuspiciousEqualsMethodAnalyzer.InstanceMembersAreNotUsedRule).WithSpan(6, 26, 6, 32),
                    },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task NoWarn_When_Only_Static_Members_Are_Used_With_Instance_Method()
        {
            // No warnings here: class with no fields or properties may be special.
            string code = @"
class FooBar
{
    private int N() => 42;

    public override bool Equals(object obj)
    {
        return System.Collections.Generic.EqualityComparer<FooBar>.Default.Equals(obj as FooBar);
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task Warn_When_Only_Static_Members_Are_Used_For_Structs()
        {
            string code = @"
class Foo {public int X = 42;}
struct FooBar
{
    private int _n;
    private static int _s = 4;
    private static Foo _f = new Foo();

    public override bool Equals(object obj)
    {
        return _f.X == _s && 
            System.Collections.Generic.EqualityComparer<FooBar>.Default.Equals(obj as FooBar);
    }
}
";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(SuspiciousEqualsMethodAnalyzer.InstanceMembersAreNotUsedRule).WithSpan(9, 26, 9, 32),
                        DiagnosticResult.CompilerError("CS0077").WithSpan(12, 80, 12, 93).WithMessage("The as operator must be used with a reference type or nullable type ('FooBar' is a non-nullable value type)"),
                    },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task NoWorn_When_Class_Has_No_Instance_Members()
        {
            string code = @"
class FooBar
{
    private static int _n = 42;
    private static int _s = 4;
    private static Foo _f = new Foo();

    public override bool Equals(object obj)
    {
        return _f.X == _s && 
            System.Collections.Generic.EqualityComparer<FooBar>.Default.Equals(obj as FooBar);
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(
                code,
                DiagnosticResult.CompilerError("CS0246").WithSpan(6, 20, 6, 23).WithMessage("The type or namespace name 'Foo' could not be found (are you missing a using directive or an assembly reference?)"),
                DiagnosticResult.CompilerError("CS0246").WithSpan(6, 33, 6, 36).WithMessage("The type or namespace name 'Foo' could not be found (are you missing a using directive or an assembly reference?)"));
        }

        [Test]
        public async Task NoWorn_When_This_Is_Used()
        {
            string code = @"
class FooBar
{
    private int _n = 42;
    public override bool Equals(object obj)
    {
        return System.Collections.Generic.EqualityComparer<FooBar>.Default.Equals(this as FooBar ?? obj);
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task NoWorn_When_other_is_used_via_pattern_matching()
        {
            string code = @"
class FooBar
{
    private int _n = 42;
    public bool Equals(FooBar other) => _n == other._n;
    public override bool Equals(object obj)
    {
        return obj is FooBar fb && Equals(fb);
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task Warn_When_Rhs_Is_Not_Referenced()
        {
            string code = @"
class Foo {public int X = 42;}
class FooBar
{
    private int _n = 42;
    private static int _s = 4;
    private static Foo _f = new Foo();

    public override bool Equals(object obj)
    {
        return _f.X == _n && 
            System.Collections.Generic.EqualityComparer<FooBar>.Default.Equals(this as FooBar);
    }
}
";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(SuspiciousEqualsMethodAnalyzer.RightHandSideIsNotUsedRule).WithSpan(9, 40, 9, 43).WithArguments("obj"),
                        VerifyCS.Diagnostic(SuspiciousEqualsMethodAnalyzer.RightHandSideIsNotUsedRule).WithSeverity(DiagnosticSeverity.Hidden).WithSpan(9, 40, 9, 43).WithMessage("bar"),
                    },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task No_Warning_When_Method_Just_Throws()
        {
            string code = @"
class FooBar
{
    private int _n = 42;

    public override bool Equals(object obj)
    {
        throw new System.Exception();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task No_Warning_When_Method_Just_Throws_Using_Expression_Body()
        {
            string code = @"
class FooBar
{
    private int _n = 42;

    public override bool Equals(object obj) =>
        throw new System.Exception();
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task No_Warn_When_GetType_Is_Called()
        {
            string code = @"
class FooBar
{
    private readonly int _x = 42;
    public override bool Equals(object obj)
    {
        return obj != null && obj.GetType() == GetType();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task No_Warn_When_EqualsBase_Is_Called()
        {
            string code = @"
class Base
{
    protected bool EqualsBase(Base other) => false;
}
class Derived : Base, System.IEquatable<Derived>
{
    public string Message {get;}
    public bool Equals(Derived other)
    {
        return EqualsBase(other) && other != null;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }
    }
}
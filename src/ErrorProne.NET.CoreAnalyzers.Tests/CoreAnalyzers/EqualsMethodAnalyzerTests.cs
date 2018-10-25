﻿using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class EqualsMethodAnalyzerTests : CSharpAnalyzerTestFixture<SuspiciousEqualsMethodAnalyzer>
    {
        public const string DiagnosticId = SuspiciousEqualsMethodAnalyzer.DiagnosticId;

        [Test]
        public void Warn_For_Strongly_Typed_Equalsl()
        {
            string code = @"
public class MyS : System.IEquatable<MyS>
{
    public int Line { get; }
    public bool [|Equals|](MyS other) => other != null;
}
";
            HasDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void NoWarn_When_For_Pattern_Matching()
        {
            string code = @"
public readonly struct MyS : System.IEquatable<MyS>
{
    public int Line { get; }

    public override int GetHashCode() => 42;
    public override bool Equals(object other) => other is MyS s && Equals(s);
    
}
";
            NoDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void NoWarn_When_For_Pattern_As()
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
            NoDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void NoWarn_When_For_Is_Plus_Cast()
        {
            string code = @"
public readonly struct MyS : System.IEquatable<MyS>
{
    public int Line { get; }

    public override int GetHashCode() => 42;
    public override bool Equals(object other) => other is MyS && Equals((MyS)s);
    public bool Equals(MyS other) => Line == other.Line;
}
";
            NoDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void Warn_When_Only_Static_Members_Are_Used()
        {
            string code = @"
class Foo {public int X = 42;}
class FooBar
{
    private int _n = 42;
    private static int _s = 4;
    private static Foo _f = new foo();

    public override bool [|Equals|](object obj)
    {
        return _f.X == _s && 
            System.Collections.Generic.EqualityComparer<FooBar>.Default.Equals(obj as FooBar);
    }
}
";
            HasDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void Warn_When_Only_Static_Members_Are_Used_In_Expression_Body_Impl()
        {
            string code = @"
class Foo {public int X = 42;}
class FooBar
{
    private int _n = 42;
    private static int _s = 4;
    private static Foo _f = new foo();

    public override bool [|Equals|](object obj)
       =>
        _f.X == _s && 
            System.Collections.Generic.EqualityComparer<FooBar>.Default.Equals(obj as FooBar);
}
";
            HasDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void Warn_When_Only_Static_Members_Are_Used_With_Instance_Props()
        {
            string code = @"
class FooBar
{
    private int N => 42;

    public override bool [|Equals|](object obj)
    {
        return System.Collections.Generic.EqualityComparer<FooBar>.Default.Equals(obj as FooBar);
    }
}
";
            HasDiagnostic(code, DiagnosticId);
        }

        class FooBar
        {
            private int N() => 42;
        }

        [Test]
        public void NoWarn_When_Only_Static_Members_Are_Used_With_Instance_Method()
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
            NoDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void Warn_When_Only_Static_Members_Are_Used_For_Structs()
        {
            string code = @"
class Foo {public int X = 42;}
struct FooBar
{
    private int _n;
    private static int _s = 4;
    private static Foo _f = new foo();

    public override bool [|Equals|](object obj)
    {
        return _f.X == _s && 
            System.Collections.Generic.EqualityComparer<FooBar>.Default.Equals(obj as FooBar);
    }
}
";
            HasDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void NoWorn_When_Class_Has_No_Instance_Members()
        {
            string code = @"
class FooBar
{
    private static int _n = 42;
    private static int _s = 4;
    private static Foo _f = new foo();

    public override bool Equals(object obj)
    {
        return _f.X == _s && 
            System.Collections.Generic.EqualityComparer<FooBar>.Default.Equals(obj as FooBar);
    }
}
";
            NoDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void NoWorn_When_This_Is_Used()
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
            NoDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void Warn_When_Rhs_Is_Not_Referenced()
        {
            string code = @"
class Foo {public int X = 42;}
class FooBar
{
    private int _n = 42;
    private static int _s = 4;
    private static Foo _f = new foo();

    public override bool Equals(object [|obj|])
    {
        return _f.X == _n && 
            System.Collections.Generic.EqualityComparer<FooBar>.Default.Equals(this as FooBar);
    }
}
";
            HasDiagnostics(code, new []{DiagnosticId, DiagnosticId + "WithoutSuggestion"});
        }

        [Test]
        public void No_Warning_When_Method_Just_Throws()
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
            NoDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void No_Warning_When_Method_Just_Throws_Using_Expression_Body()
        {
            string code = @"
class FooBar
{
    private int _n = 42;

    public override bool Equals(object obj) =>
        throw new System.Exception();
}";
            NoDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void No_Warn_When_GetType_Is_Called()
        {
            string code = @"
class FooBar
{
    public override bool Equals(object obj)
    {
        return obj != null && obj.GetType() == this.GetType();
    }
}";
            NoDiagnostic(code, DiagnosticId);
        }
    }
}
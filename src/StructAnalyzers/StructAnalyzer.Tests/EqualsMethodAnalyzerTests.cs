using ErrorProne.NET.Structs;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace StructAnalyzers.Tests
{
    [TestFixture]
    public class EqualsMethodAnalyzerTests : CSharpAnalyzerTestFixture<EqualsMethodAnalyzer>
    {
        public const string DiagnosticId = EqualsMethodAnalyzer.DiagnosticId;

        [Test]
        public void WarnWhenReferencesStaticOnly()
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
        public void WarnWhenReferencesStaticOnlyForStructs()
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
        public void ShouldNotWarnWhenThereIsNoInstanceMembers()
        {
            string code = @"
class FooBar
{
    private staticint _n = 42;
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
        public void WarnWhenNotReferencesTheRightHandSide()
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
    }
}
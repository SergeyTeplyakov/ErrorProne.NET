using System;
using System.Linq;
using ErrorProne.NET.Common;
using ErrorProne.NET.SideEffectRules;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.SideEffectRules
{
    [TestFixture]
    public class UnobservedPureMethodInvocationAnalyzerTests : CSharpAnalyzerTestFixture<UnobservedPureMethodInvocationAnalyzer>
    {
        [Test]
        public void ShouldWarnOnEnumerable()
        {
            const string code = @"
using System.Linq;
class C
{
    public C()
    {
        var x1 = Enumerable.Range(1, 10);
        [|x1.Select(x => x.ToString())|];
    }
}";

            HasDiagnostic(code, RuleIds.UnobservedPureMethodInvocationId);
        }

        [Test]
        public void ShouldWarnOnString()
        {
            const string code = @"
using System.Linq;
class C
{
    public C()
    {
        var x = ""foo"";
        [|x.Substring(1)|];
    }
}";

            HasDiagnostic(code, RuleIds.UnobservedPureMethodInvocationId);
        }

        [Test]
        public void ShouldWarnOnMethodsFromSystemObject()
        {
            const string code = @"
using System.Linq;
class C
{
    public C()
    {
        [|GetHashCode()|];
    }
    public override int GetHashCode() {return 42;}
}";

            HasDiagnostic(code, RuleIds.UnobservedPureMethodInvocationId);
        }

        [Test]
        public void ShouldWarnOnDerivedFromIEquatableOfT()
        {
            const string code = @"
class C : System.IEquatable<C>
{
    public bool Equals(C other)
    {
        return false;
    }

    public static void Test()
    {
        C c = new C();
        [|c.Equals(new C())|];
    }
}";

            HasDiagnostic(code, RuleIds.UnobservedPureMethodInvocationId);
        }

        [Test]
        public void ShouldWarnOnIEquatableOfT()
        {
            const string code = @"
class C : System.IEquatable<C>
{
    public bool Equals(C other)
    {
        return false;
    }

    public static void Test()
    {
        System.IEquatable<C> c = new C();
        [|c.Equals(new C())|];
    }
}";

            HasDiagnostic(code, RuleIds.UnobservedPureMethodInvocationId);
        }

        [Test]
        public void ShouldWarnOnMethodWithPureAttributeOnSourceLevel()
        {
            const string code = @"
class C
{
    [System.Diagnostics.Contracts.Pure]
    public int Foo() {return 42;}
    public C()
    {
        [|Foo()|];
    }
}";

            HasDiagnostic(code, RuleIds.UnobservedPureMethodInvocationId);
        }

        [Test]
        public void ShouldWarnOnMethodWithPureAttributeOnMetadataLevel()
        {
            
            const string code = @"
class C
{
    public C()
    {
        [|System.Char.IsUpper('c')|];
    }
}";

            HasDiagnostic(code, RuleIds.UnobservedPureMethodInvocationId);
        }

        [Test]
        public void ShouldWarnOnRoslynApi()
        {
            const string code = @"
namespace Microsoft.CodeAnalysis {
class C
{
    private static void Test()
    {
        var c = new C();
        [|c.WithX(42)|];
    }
   
    public C Update(int number) {return this;}
}
}";

            HasDiagnostic(code, RuleIds.UnobservedPureMethodInvocationId);
        }

        [Test]
        public void ShouldWarnOnWithApi()
        {
            const string code = @"
class C
{
    private static void Test()
    {
        var c = new C();
        [|c.WithX(42)|];
    }
   
    public C WithX(int number) {return this;}
}";

            HasDiagnostic(code, RuleIds.UnobservedPureMethodInvocationId);
        }
    }
}
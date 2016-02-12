using ErrorProne.NET.Common;
using ErrorProne.NET.SideEffectRules;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.ExceptionRulesTests
{
    [TestFixture]
    public class SideEffectFreeExceptionCreationConstructionAnalyzerTests : CSharpAnalyzerTestFixture<SideEffectFreeExceptionConstructionAnalyzer>
    {
        [Test]
        public void ShouldWarnOnException()
        {
            const string code = @"
class C
{
    public C()
    {
        [|new System.Exception()|];
    }
}";

            HasDiagnostic(code, RuleIds.SideEffectFreeExceptionContructionId);
        }

        [Test]
        public void ShouldWarnOnArgumentException()
        {
            const string code = @"
class C
{
    public C()
    {
        [|new System.ArgumentException()|];
    }
}";

            HasDiagnostic(code, RuleIds.SideEffectFreeExceptionContructionId);
        }

        [Test]
        public void ShouldNotWarnOnNewString()
        {
            
            const string code = @"
class C
{
    public C()
    {
        [|new System.Text.StringBuilder();|]
    }
}";

            NoDiagnostic(code, RuleIds.SideEffectFreeExceptionContructionId);
        }

        [Test]
        public void ThrowShouldNotTrigger()
        {
            const string code = @"
class C
{
    public C()
    {
        throw new System.Exception();
    }
}";

            NoDiagnostic(code, RuleIds.SideEffectFreeExceptionContructionId);
        }

        [Test]
        public void ShouldNotWarnOnLocalAssignment()
        {
            const string code = @"
class C
{
    public C()
    {
        var x = new System.Exception();
    }
}";

            NoDiagnostic(code, RuleIds.SideEffectFreeExceptionContructionId);
        }

        [Test]
        public void ShouldNotWarnOnMethodPassing()
        {
            const string code = @"
class C
{
    public C()
    {
        Foo(new System.Exception();
    }
    private void Foo(System.Exception e) {}
}";

            NoDiagnostic(code, RuleIds.SideEffectFreeExceptionContructionId);
        }
    }
}
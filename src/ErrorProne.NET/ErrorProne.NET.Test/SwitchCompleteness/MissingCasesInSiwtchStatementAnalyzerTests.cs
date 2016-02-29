using ErrorProne.NET.Common;
using ErrorProne.NET.Rules.SwitchAnalysis;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.SwitchCompleteness
{
    [TestFixture]
    public class MissingCasesInSiwtchStatementAnalyzerTests : CSharpAnalyzerTestFixture<MissingCasesInSwitchStatementAnalyzer>
    {
        [Test]
        public void ShouldWarnWhenDefaultThrowsNotImplementedException()
        {
            const string code = @"
enum SomeEnum
{
	Case1,
	Case2,
	Case3 = Case1,
	Case4
}
class Foo
{
	public void Test(SomeEnum se)
    {
        [|switch|](se)
        {
            case SomeEnum.Case1: break;
            case (SomeEnum)42: break;
            default: throw new System.InvalidOperationException(); break;
        }
    }
}";

            HasDiagnostic(code, RuleIds.MissingCasesInSwitchStatement);
        }

        [Test]
        public void ShouldNotWarnWhenAllCasesAreCovered()
        {
            const string code = @"
enum SomeEnum
{
	Case1,
	Case2,
}
class Foo
{
	public void Test(SomeEnum se)
    {
        switch(se)
        {
            case SomeEnum.Case1: break;
            case SomeEnum.Case2: break;
            default: throw new System.InvalidOperationException(); break;
        }
    }
}";

            NoDiagnostic(code, RuleIds.MissingCasesInSwitchStatement);
        }
        
        [Test]
        public void NoWarningIfSwitchCoversEverything()
        {
            const string code = @"
enum SomeEnum : byte
{
	Case1,
	Case2,
}
class Foo
{
	public void Test(SomeEnum se)
    {
        switch(se)
        {
            case SomeEnum.Case1: break;
            case SomeEnum.Case2: break;
            default: System.Diagnostics.Contracts.Contract.Assert(false); break;
        }
    }
}";

            NoDiagnostic(code, RuleIds.MissingCasesInSwitchStatement);
        }

        [Test]
        public void ShouldWarnOnField()
        {
            const string code = @"
enum SomeEnum
{
	Case1,
	Case2,
	Case3 = Case1,
	Case4
}
class Foo
{
	public void Test()
    {
        SomeEnum se = SomeEnum.Case1;
        [|switch|](se)
        {
            case SomeEnum.Case1: break;
            case (SomeEnum)42: break;
            default: System.Diagnostics.Contracts.Contract.Assert(false);break;
        }
    }
}";

            HasDiagnostic(code, RuleIds.MissingCasesInSwitchStatement);
        }

        [Test]
        public void SouldNotWarnOnBothConsts()
        {
            const string code = @"
enum SomeEnum
{
	Case1,
	Case2,
	Case3 = Case1,
	Case4
}
class Foo
{
	public void Test(SomeEnum se)
    {
        switch(se)
        {
            case (SomeEnum)0: break;
            case (SomeEnum)1: break;
            default: throw new System.InvalidOperationException();
        }
    }
}";

            NoDiagnostic(code, RuleIds.MissingCasesInSwitchStatement);
        }

        [Test]
        public void NoWarningWhtnDefaultDoesntThrow()
        {
            const string code = @"
enum SomeEnum
{
	Case1,
	Case2,
	Case3 = Case1,
	Case4
}
class Foo
{
	public void Test(SomeEnum se)
    {
        switch(se)
        {
            case SomeEnum.Case1: break;
            case (SomeEnum)42: break;
            default: System.Console.WriteLine(""Missed case""); break;
        }
    }
}";

            NoDiagnostic(code, RuleIds.MissingCasesInSwitchStatement);
        }
    }
}
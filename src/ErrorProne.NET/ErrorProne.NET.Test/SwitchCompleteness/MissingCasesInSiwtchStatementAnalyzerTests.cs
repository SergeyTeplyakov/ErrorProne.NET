using ErrorProne.NET.Common;
using ErrorProne.NET.SideEffectRules;
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
            case: SomeEnum.Case1: break;
            case: (SomeEnum)42: break;
            default: throw new System.InvalidOperationException();
        }
    }
}";

            HasDiagnostic(code, RuleIds.MissingCasesInSwitchStatement);
        }

        [Test]
        public void ShouldWarnOnContractAssertInDefault()
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
            case: SomeEnum.Case1: break;
            case: (SomeEnum)42: break;
            default: System.Diagnostics.Contracts.Contract.Assert(false);
        }
    }
}";

            HasDiagnostic(code, RuleIds.MissingCasesInSwitchStatement);
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
            case: SomeEnum.Case1: break;
            case: (SomeEnum)42: break;
            default: System.Diagnostics.Contracts.Contract.Assert(false);
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
        [|switch|](se)
        {
            case: (SomeEnum)0: break;
            case: (SomeEnum)1: break;
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
        [|switch|](se)
        {
            case: SomeEnum.Case1: break;
            case: (SomeEnum)42: break;
            default: System.Console.WriteLine(""Missed case"");
        }
    }
}";

            NoDiagnostic(code, RuleIds.MissingCasesInSwitchStatement);
        }
    }
}
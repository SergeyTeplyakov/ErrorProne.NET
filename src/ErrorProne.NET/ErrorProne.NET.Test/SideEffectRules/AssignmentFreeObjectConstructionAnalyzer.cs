using ErrorProne.NET.Common;
using ErrorProne.NET.SideEffectRules;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.SideEffectRules
{
    [TestFixture]
    public class AssignmentFreeObjectConstructionAnalyzerTests : CSharpAnalyzerTestFixture<AssignmentFreeImmutableObjectConstructionAnalyzer>
    {
        [Test]
        public void ShouldWarnOnObject()
        {
            const string code = @"
class C
{
    public C()
    {
        [|new object()|];
    }
}";

            HasDiagnostic(code, RuleIds.AssignmentFreeImmutableObjectContructionId);
        }

        [Test]
        public void NoWarningWithUsingStatement()
        {
            const string code = @"
class Foo : IDisposable
{
	public void Dispose() { }
	public static void Test()
	{
		using (new Foo()) {}
	}
}";

            NoDiagnostic(code, RuleIds.AssignmentFreeImmutableObjectContructionId);
        }

        [Test]
        public void NoWarningWithFunctionInvocation()
        {
            // This could be a warning if Blah is pure and does provide a value!
            const string code = @"
class Foo
{
	public void Blah() { }

	public static void Test()
	{
		new Foo().Blah();
	}
}";

            NoDiagnostic(code, RuleIds.AssignmentFreeImmutableObjectContructionId);
        }
    }
}
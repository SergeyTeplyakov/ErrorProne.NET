using ErrorProne.NET.Common;
using ErrorProne.NET.SideEffectRules;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.ExceptionRulesTests
{
    [TestFixture]
    public class AssignmentFreeObjectConstructionAnalyzerTests : CSharpAnalyzerTestFixture<AssignmentFreeObjectConstructionAnalyzer>
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

            HasDiagnostic(code, RuleIds.AssignmentFreeObjectContructionId);
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

            NoDiagnostic(code, RuleIds.AssignmentFreeObjectContructionId);
        }
    }
}
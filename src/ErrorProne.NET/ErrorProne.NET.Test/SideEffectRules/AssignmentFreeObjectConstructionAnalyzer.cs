using System.Collections.Generic;
using ErrorProne.NET.Common;
using ErrorProne.NET.SideEffectRules;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.SideEffectRules
{
    enum Foo { }
    [TestFixture]
    public class AssignmentFreeObjectConstructionAnalyzerTests : CSharpAnalyzerTestFixture<AssignmentFreeImmutableObjectConstructionAnalyzer>
    {
        [TestCaseSource(nameof(GetWarnTestCases))]
        public void ShouldWarn(string code)
        {
            HasDiagnostic(code, RuleIds.AssignmentFreeImmutableObjectContructionId);
        }

        public static IEnumerable<string> GetWarnTestCases()
        {
            // Should warn on object
            yield return @"
class C
{
    public C()
    {
        [|new object()|];
    }
}";
            // Should warn on other immutable types
            yield return @"
class C
{
    public C()
    {
        [|new string()|];
    }
}";

            // Should warn on collections
            yield return @"
class C
{
    public C()
    {
        [|new System.Collections.Generic.List<int>()|];
    }
}";
            // Should warn on default constructors for value types
            yield return @"
class C
{
    public C()
    {
        [|new int()|];
    }
}";
            
            // For custom structs as well
            yield return @"
struct Foo {}
class C
{
    public C()
    {
        [|new Foo()|];
    }
}";

            // Should war on enums
            yield return @"
enum Foo {}
class C
{
    public C()
    {
        [|new Foo()|];
    }
}";
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
		new object().ToString();
	}
}";

            NoDiagnostic(code, RuleIds.AssignmentFreeImmutableObjectContructionId);
        }
    }
}
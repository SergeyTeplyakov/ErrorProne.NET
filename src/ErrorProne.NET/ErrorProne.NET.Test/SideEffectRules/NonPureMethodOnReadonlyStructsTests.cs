using ErrorProne.NET.Common;
using ErrorProne.NET.SideEffectRules;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.SideEffectRules
{
    [TestFixture]
    public class NonPureMethodOnReadonlyStructsTests : CSharpAnalyzerTestFixture<NonPureMethodsOnReadonlyStructs>
    {
        [Test]
        public void WarnMutableStructStoredAsReadonlyField()
        {
            const string code = @"
struct Mutable
{
	private string s;

	public void Mutate()
	{
		DoMutation();
	}
	
	private void DoMutation()
	{
		s += ""1"";

    }

        public string S => s;
}
    class Foo
    {
        public readonly Mutable m;
        public static void Test()
        {
            var foo = new Foo();
            [|foo.m.Mutate()|];
        }
    }";

            HasDiagnostic(code, RuleIds.NonPureMethodsOnReadonlyStructs);
        }

        [Test]
        public void WarnOnListEnumerator()
        {
            const string code = @"
class Test
    {
        public readonly System.Collections.Generic.List<int>.Enumerator e = 
            new System.Collections.Generic.List<int> {1, 2}.GetEnumerator();

        public void Warn()
        {
            [|e.MoveNext()|];
            [|e.Dispose()|];
        }
    }";

            HasDiagnostic(code, RuleIds.NonPureMethodsOnReadonlyStructs);
        }

        [Test]
        public void ShouldNotWarnOnImmutableStruct()
        {
            const string code = @"
struct Immutable
{
	private readonly string s;

	public void PrintToConsole()
	{}
    public string S => s;
}
    class Foo
    {
        public readonly Immutable m;
        public static void Test()
        {
            var foo = new Foo();
            foo.m.PrintToConsole();
        }
    }";

            NoDiagnostic(code, RuleIds.NonPureMethodsOnReadonlyStructs);
        }
    }
}
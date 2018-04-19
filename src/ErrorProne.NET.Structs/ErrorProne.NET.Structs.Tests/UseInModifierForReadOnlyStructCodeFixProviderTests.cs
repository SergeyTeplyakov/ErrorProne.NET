using System.Collections.Generic;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Structs.Tests
{
    [TestFixture]
    public class UseInModifierForReadOnlyStructCodeFixProviderTests : CSharpCodeFixTestFixture<UseInModifierForReadOnlyStructCodeFixProvider>
    {
        [Test]
        public void AddInModifier()
        {
            string code = @"readonly struct FooBar { public static void Foo([|FooBar fb|]) {} }";

            string expected = @"readonly struct FooBar { public static void Foo(in FooBar fb) {} }";

            TestCodeFix(code, expected, UseInModifierForReadOnlyStructAnalyzer.Rule);
        }

        [Test]
        public void AddInModifierWithTrivia()
        {
            string code = @"readonly struct FooBar
{
    public static void Foo(
        [|FooBar fb|])
    {
    }
}";

            string expected = @"readonly struct FooBar
{
    public static void Foo(
        in FooBar fb)
    {
    }
}";

            TestCodeFix(code, expected, UseInModifierForReadOnlyStructAnalyzer.Rule);
        }

        [Test]
        public void NoCodeFixWhenUsedAsOut()
        {
            string code = @"
public readonly struct RS
{
    public static void UsedAsIn([|RS rs|])
    {
        UseAsOut(out rs);
    }

    public static void UseAsOut(out RS rs) => rs = default;
}";
            TestNoCodeFix(code, UseInModifierForReadOnlyStructAnalyzer.Rule);
        }

        [Test]
        public void NoCodeFixWhenUsedAsRef()
        {
            string code = @"
public readonly struct RS
{
    public static void UsedAsIn([|RS rs|])
    {
        UseAsOut(ref rs);
    }

    public static void UseAsOut(ref RS rs) => rs = default;
}";
            TestNoCodeFix(code, UseInModifierForReadOnlyStructAnalyzer.Rule);
        }

        //[TestCaseSource(nameof(GetDoNothingCodeFixes))]
        public void DoNothingCodeFixes(string code)
        {
            TestNoCodeFix(code, UseInModifierForReadOnlyStructAnalyzer.Rule);
        }

        public static IEnumerable<string> GetDoNothingCodeFixes()
        {
            // Captured in indexer
            yield return @"readonly struct FooBar { }
        class FooClass { public System.Func<FooBar> this[[|FooBar fb|]] => () => fb; }";

            // Captured in anonymous delegate
            yield return @"readonly struct FooBar { public static void Foo([|FooBar fb|]) { System.Func<FooBar> a = delegate(){ return fb;}; } }";

            // Captured in lambda
            yield return @"readonly struct FooBar { public static void Foo([|FooBar fb|]) {System.Func<FooBar> a = () => fb; } }";

            // Captured in lambda2
            yield return @"readonly struct FooBar { public static System.Func<FooBar> Foo([|FooBar fb|]) => () => fb; }";
        }
    }
}
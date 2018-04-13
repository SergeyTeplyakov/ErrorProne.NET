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
    }
}
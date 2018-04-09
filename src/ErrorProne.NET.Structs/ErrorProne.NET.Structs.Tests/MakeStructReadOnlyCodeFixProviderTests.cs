using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Structs.Test
{
    [TestFixture]
    public class MakeStructReadOnlyCodeFixProviderTests : CSharpCodeFixTestFixture<MakeStructReadOnlyCodeFixProvider>
    {
        [Test]
        public void ConvertSingleAwait()
        {
            string code = @"struct [|FooBar|] {}";

            string expected = @"readonly struct FooBar {}";

            TestCodeFix(code, expected, MakeStructReadOnlyAnalyzer.Rule);
        }
    }
}
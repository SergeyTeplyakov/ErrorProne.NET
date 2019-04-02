using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.StructAnalyzers.Tests
{
    [TestFixture]
    public class MakeStructReadOnlyCodeFixProviderTests : CSharpCodeFixTestFixture<MakeStructReadOnlyCodeFixProvider>
    {
        [Test]
        public void MakeStructReadOnly()
        {
            string code = @"struct [|FooBar|] {}";

            string expected = @"readonly struct FooBar {}";

            TestCodeFix(code, expected, MakeStructReadOnlyAnalyzer.Rule);
        }

        [Test]
        public void MakePartialStructReadOnly()
        {
            string code = @"internal partial struct [|FooBar|] {}";

            string expected = @"internal readonly partial struct FooBar {}";

            TestCodeFix(code, expected, MakeStructReadOnlyAnalyzer.Rule);
        }

        [Test]
        public void MakeStructReadOnlyForPublicStruct()
        {
            string code = @"public struct [|FooBar|] {}";

            string expected = @"public readonly struct FooBar {}";

            TestCodeFix(code, expected, MakeStructReadOnlyAnalyzer.Rule);
        }

        [Test]
        public void MakeStructReadOnlyForPublicStructWithComment()
        {
            string code = @"/// <summary>
/// comment
/// </summary>
public struct [|FooBar|] {}";

            string expected = @"/// <summary>
/// comment
/// </summary>
public readonly struct FooBar {}";

            TestCodeFix(code, expected, MakeStructReadOnlyAnalyzer.Rule);
        }

        [Test]
        public void MakeStructReadOnlyForPublicPartialStruct()
        {
            string code = @"public partial struct [|FooBar|] {}";

            string expected = @"public readonly partial struct FooBar {}";

            TestCodeFix(code, expected, MakeStructReadOnlyAnalyzer.Rule);
        }
    }
}
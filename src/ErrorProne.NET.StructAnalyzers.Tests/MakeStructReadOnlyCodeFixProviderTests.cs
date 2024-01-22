using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.StructAnalyzers.MakeStructReadOnlyAnalyzer,
    ErrorProne.NET.StructAnalyzers.MakeStructReadOnlyCodeFixProvider>;

namespace ErrorProne.NET.StructAnalyzers.Tests
{
    [TestFixture]
    public class MakeStructReadOnlyCodeFixProviderTests
    {
        [Test]
        public async Task MakeStructReadOnly()
        {
            string code = @"struct [|FooBar|] {}";

            string expected = @"readonly struct FooBar {}";

            await VerifyCS.VerifyCodeFixAsync(code, expected);
        }

        [Test]
        public async Task MakePartialStructReadOnly()
        {
            string code = @"internal partial struct [|FooBar|] {}";

            string expected = @"internal readonly partial struct FooBar {}";

            await VerifyCS.VerifyCodeFixAsync(code, expected);
        }

        [Test]
        public async Task MakeStructReadOnlyForPublicStruct()
        {
            string code = @"public struct [|FooBar|] {}";

            string expected = @"public readonly struct FooBar {}";

            await VerifyCS.VerifyCodeFixAsync(code, expected);
        }

        [Test]
        public async Task MakeStructReadOnlyForPublicStructWithComment()
        {
            string code = @"/// <summary>
/// comment
/// </summary>
public struct [|FooBar|] {}";

            string expected = @"/// <summary>
/// comment
/// </summary>
public readonly struct FooBar {}";

            await VerifyCS.VerifyCodeFixAsync(code, expected);
        }

        [Test]
        public async Task MakeStructReadOnlyForPublicPartialStruct()
        {
            string code = @"public partial struct [|FooBar|] {}";

            string expected = @"public readonly partial struct FooBar {}";

            await VerifyCS.VerifyCodeFixAsync(code, expected);
        }
    }
}
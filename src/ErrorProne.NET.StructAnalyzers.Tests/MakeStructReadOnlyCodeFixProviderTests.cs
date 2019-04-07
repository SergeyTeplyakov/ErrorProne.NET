using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
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

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expected } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task MakePartialStructReadOnly()
        {
            string code = @"internal partial struct [|FooBar|] {}";

            string expected = @"internal readonly partial struct FooBar {}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expected } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task MakeStructReadOnlyForPublicStruct()
        {
            string code = @"public struct [|FooBar|] {}";

            string expected = @"public readonly struct FooBar {}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expected } },
            }.WithoutGeneratedCodeVerification().RunAsync();
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

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expected } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task MakeStructReadOnlyForPublicPartialStruct()
        {
            string code = @"public partial struct [|FooBar|] {}";

            string expected = @"public readonly partial struct FooBar {}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expected } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
    }
}
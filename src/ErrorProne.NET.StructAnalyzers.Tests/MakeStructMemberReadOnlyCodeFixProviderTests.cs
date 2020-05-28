using ErrorProne.NET.TestHelpers;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.StructAnalyzers.MakeStructMemberReadOnlyAnalyzer,
    ErrorProne.NET.StructAnalyzers.MakeStructMemberReadOnlyCodeFixProvider>;

namespace ErrorProne.NET.StructAnalyzers.Tests
{
    [TestFixture]
    public class MakeStructMemberReadOnlyCodeFixProviderTests
    {
        [Test]
        public async Task MakePropertyReadOnly()
        {
            string code = @"struct Test {
    private int x;
    public int [|X|] => 42;
}";

            string expected = @"struct Test {
    private int x;
    public readonly int X => 42;
}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expected } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
        
        [Test]
        public async Task MakePropertyWithBodyReadOnly()
        {
            string code = @"struct Test {
    private int x;
    public int [|X|] { get=> 42; }
}";

            string expected = @"struct Test {
    private int x;
    public readonly int X { get=> 42; }
}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expected } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
        
        [Test]
        public async Task MakeMethodReadOnly()
        {
            string code = @"struct Test {
    private int x;
    public int [|X|]() => 42;
}";

            string expected = @"struct Test {
    private int x;
    public readonly int X() => 42;
}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expected } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
        
        [Test]
        public async Task MakeMethodWithBodyReadOnly()
        {
            string code = @"struct Test {
    private int x;
    public int [|X|]() { return 42; }
}";

            string expected = @"struct Test {
    private int x;
    public readonly int X() { return 42; }
}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expected } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
        
        [Test]
        public async Task MakeToStringWithBodyReadOnly()
        {
            string code = @"struct Test {
    private int x;
    public override string [|ToString|]() => string.Empty;
}";

            string expected = @"struct Test {
    private int x;
    public override readonly string ToString() => string.Empty;
}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expected } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
    }
}
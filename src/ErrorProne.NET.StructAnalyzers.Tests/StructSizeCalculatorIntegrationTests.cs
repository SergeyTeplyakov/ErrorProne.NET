using System.Threading.Tasks;
using ErrorProne.NET.TestHelpers;
using NUnit.Framework;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.StructAnalyzers.UseInModifierForReadOnlyStructAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.StructAnalyzers.Tests
{
    /// <summary>
    /// There is no simple way to test StructSizeCalculator, so we'll some analyzer tests for that.
    /// </summary>
    [TestFixture]
    public class StructSizeCalculatorIntegrationTests
    {
        [Test]
        public async Task GetOnlyPropertyShouldBeIgnoredBySizeCalculator()
        {
            string code = @"
readonly struct S { private readonly object _o1, _o2, _o3; public object Foo => null; }
class Foo {
   public static void Bar(S s) {}
}";

            await new VerifyCS.Test
            {
                TestCode = code,
            }.WithLargeStructThreshold(4 * sizeof(long)).RunAsync();
        }

        [Test]
        public async Task StructLayoutShouldBeRespected()
        {
            string code = @"
using System.Runtime.InteropServices;
[StructLayout(LayoutKind.Sequential, Size = 24)]
readonly struct S
{
    public readonly byte FixedElementField;
}
class Foo {
   public static void Bar([|S s|]) {}
}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }
                .WithoutGeneratedCodeVerification()
                .WithLargeStructThreshold(24)
                .RunAsync();
        }
        
        [Test]
        public async Task StructLayoutShouldBeRespectedForNestedStruct()
        {
            string code = @"
using System.Runtime.InteropServices;
[StructLayout(LayoutKind.Sequential, Size = 24)]
readonly struct S
{
    public readonly byte FixedElementField;
}

readonly struct S2 {
  private readonly S s;
  private readonly long l;
}
class Foo {
   public static void Bar([|S2 s|]) {}
}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }
                .WithoutGeneratedCodeVerification()
                .WithLargeStructThreshold(1)
                .RunAsync();
        }
        
        [Test]
        public async Task StructLayoutShouldBeRespectedOnlyWhenStructIsSmall()
        {
            string code = @"
using System.Runtime.InteropServices;
[StructLayout(LayoutKind.Sequential, Size = 10)]
readonly struct S
{
    public readonly long l1, l2, l3;
}
class Foo {
   public static void Bar([|S s|]) {}
}";


            await new VerifyCS.Test
                {
                    TestState = { Sources = { code } },
                }
                .WithoutGeneratedCodeVerification()
                .WithLargeStructThreshold(24)
                .RunAsync();
        }
    }
}
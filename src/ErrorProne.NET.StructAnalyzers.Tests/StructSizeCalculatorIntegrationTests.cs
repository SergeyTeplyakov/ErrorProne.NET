using System;
using ErrorProne.NET.TestHelpers;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        [SetUp]
        public void Initializer()
        {
            Settings.SetLargeStructThresholdForTestingPurposesOnly(3 * IntPtr.Size);
        }

        [Test]
        public async Task GetOnlyPropertyShouldBeIgnoredBySizeCalculator()
        {
            string code = @"
readonly struct S { private readonly object _o1, _o2; public object Foo => null; }
class Foo {
   public static void Bar(S s) {}
}";
            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task ArraySegmentShouldBeFine()
        {
            string code = @"
class Foo {
   public static void Bar(System.ArraySegment<byte> s) {}
}";
            await VerifyCS.VerifyAsync(code);
        }
    }
}
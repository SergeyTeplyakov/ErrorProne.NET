using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.Allocations.DelegateAllocationAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class DelegateAllocationAnalyzerTests
    {
        [Test]
        public async Task Delegate_Allocation_For_Simple_Lambda()
        {
            await ValidateCodeAsync(@"
class A {
    public void Foo()
    {
        System.Action a = () [|=>|] Foo();
    }
}");
        }

        [Test]
        public async Task Delegate_Allocation_For_Method_Group_Conversion()
        {
            await ValidateCodeAsync(@"
class A {
    public void Foo()
    {
        System.Action a = [|Foo|];
        System.Action a2 = [|localFunction|];
        Bar([|Foo|]);
        void localFunction() {}
    }

    private void Bar(System.Action a) {}
}");
        }

        [Test]
        public async Task No_Delegate_Allocation_For_Non_Capturing_Lambda()
        {
            await ValidateCodeAsync(@"
class A {
    public void Foo()
    {
        System.Action a = () => Bar(staticValue, GetValue());
    }

    private static void Bar(int x, int y) {}
    private static int staticValue = 42;
    private static int GetValue() => 42;
}");
        }

        private Task ValidateCodeAsync(string code)
        {
            return new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                },
            }.WithoutGeneratedCodeVerification().WithHiddenAllocationsAttributeDeclaration().WithAssemblyLevelHiddenAllocationsAttribute().RunAsync();
        }
    }
}
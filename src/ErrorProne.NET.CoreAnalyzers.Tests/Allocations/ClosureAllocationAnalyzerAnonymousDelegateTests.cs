using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.Allocations.ClosureAllocationAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class ClosureAllocationAnalyzerAnonymousDelegateTests
    {
        [Test]
        public async Task Closure_Is_Allocated_In_Method()
        {

            await ValidateCodeAsync(@"
class A {
    public void CapturedLocal(int v)
    [|{|]
        // Closure allocation here
        int n = 42;
        // Delegate allocation here.
        System.Func<int, int> a = delegate(int l) {return n + v + l;};
        a(42);
    }
}");
        }

        [Test]
        public async Task Closure_Is_Allocated_In_Constructor()
        {
            await ValidateCodeAsync(@"
class A {
    public A(int v)
    [|{|]
        // Closure allocation here
        int n = 42;
        // Delegate allocation here.
        System.Func<int, int> a = delegate(int l) {return n + v + l;};
        a(42);
    }
}");
        }

        [Test]
        public async Task Closure_Is_Allocated_In_Method_With_Expression_Body()
        {
            await ValidateCodeAsync(@"
class A {
    private int x = 42;
    public System.Func<int> ProducesFunc(int x) [|=>|] delegate() {return x;};
}");
        }

        [Test]
        public async Task Closures_Are_Allocated_In_3_Scopes()
        {
            await ValidateCodeAsync(@"
class A {
    public void F(int n)
    [|{|]
        // Closure allocation
        int scope1 = 1;
        [|{|]
            // Closure allocation
        	int scope2 = 2;
        
        	if (n > 10)
        	[|{|]
                // Closure allocation!
            	int scope3 = 3;
            	// Delegate allocation
            	System.Func<int> f2 = delegate() {return scope1 + scope2 + scope3;};
            	f2();
        	}
        }
    }
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
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
    }
}
using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.Allocations.ClosureAllocationAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class ClosureAllocationAnalyzerLocalFunctionTests
    {
        [Test]
        public async Task Closure_Is_Allocated_When_Local_Function_Is_Converted_To_Delegate()
        {
            await ValidateCodeAsync(@"
using System;
using System.Collections.Generic;
class A {
    public void M(int arg)
    [|{|]
        // This will cause the closure to be a class, not a struct
        if (arg > 2) 
        {
            Func<int> fn = local;
        }
        
        int v = local();
        int local() => arg;
    }
}");
        }

        [Test]
        public async Task Closure_Is_Allocated_When_Local_Function_Is_Converted_To_Delegate2()
        {
            await ValidateCodeAsync(@"
using System;
using System.Collections.Generic;
class A {
    private void Foo(Func<int> f) {}
    public void M(int arg)
    [|{|]
        // This will cause the closure to be a class, not a struct
        if (arg > 2) 
        {
            Foo(local);
        }
        
        int v = local();
        int local() => arg;
    }
}");
        }


        [Test]
        public async Task No_Closure_Is_Allocated_When_Local_Function_Captures_This_Only()
        {
            await ValidateCodeAsync(@"
using System;
using System.Collections.Generic;
class A {
    private int x = 42;
    private void Foo(Func<int> f) {}
    public void M(int arg)
    {
        // No closure allocations, because the local function captures 'this' but nothing else.
        // In this case the delegate is allocated at Foo(local), but no closure is created.
        if (arg > 2) 
        {
            Foo(local);
        }
        
        int v = local();
        int local() => x;
    }
}");
        }

        [Test]
        public async Task No_Closure_Is_Allocated_When_Local_Function_Is_Not_Converted_To_Delegate()
        {
            await ValidateCodeAsync(@"
using System;
using System.Collections.Generic;
class A {
    public void M(int arg)
    {
        int v = local();
        int local() => arg;
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
            }.WithoutGeneratedCodeVerification().WithHiddenAllocationsAttributeDeclaration().WithAssemblyLevelHiddenAllocationsAttribute().RunAsync();
        }
    }
}
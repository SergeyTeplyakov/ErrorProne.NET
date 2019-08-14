using System;
using System.Collections.Generic;
using NUnit.Framework;
using System.Threading.Tasks;
using ErrorProne.NET.CoreAnalyzers.Allocations;
using ErrorProne.NET.CoreAnalyzers.Tests.Allocations;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class ClosureAllocationAnalyzerTests
    {
        static Task ValidateCodeAsync(string code) => AllocationTestHelper.VerifyCodeAsync<ClosureAllocationAnalyzer>(code);

        public void ClosureAllocations(int arg)
        {
            // Delegate allocation for 'n'
            int n = 42;
            int k = 1;
            
            //// Delegate allocation for a.
            Func<int> a = () => n + k + 1;

            // Delegate allocation for 'this'
            Action a2 = () => ClosureAllocations(42);
        }

        private void Foo(int[] numbers)
        {
            int x = 42;
            List<Action> actions = new List<Action>();
            foreach (var n in numbers)
            {
                actions.Add(() => Console.WriteLine(x + n));
            }
        }

        public void AllocationLocalFunctionForLocalFunction2()
        {
            // Closure allocation
            int n = 42;
            int local() => n;
            local();

            //Func<int> f = local;
            //f();
        }

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
        System.Func<int, int> a = l => n + v + l;
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
        System.Func<int, int> a = l => n + v + l;
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
    public System.Func<int> ProducesFunc(int x) [|=>|] () => x;
}");
        }

        [Test]
        public async Task Closure_Is_Allocated_In_Properties()
        {
            await ValidateCodeAsync(@"
class A {
    private int x = 42;
    // No closure allocations, because lambda captures only 'this'.
    public bool P1 => ((System.Func<bool>)(() => x > 42))();
    
    public int P2 
    {
        // No closure allocations, because lambda captures only 'this'.
        get => ((System.Func<int>)(() => x > 42 ? 1 : 2))();
        set [|=>|] ((System.Func<bool>)(() => value > 42))();
    }
    
    public bool P3
    {
        get [|{|] int z = 42;return ((System.Func<bool>)(() => z > 42))(); }
    }
}");
        }

        [Test]
        public async Task Closure_Is_Allocated_In_If_Block()
        {
            await ValidateCodeAsync(@"
class A {
    public void M(int y)
    {
        if (y > 0)
        [|{|]
            // Closure allocation
            int x = 42;
            System.Func<int> f = () => x;
            f();
        }
    }
}");
        }

        [Test]
        public async Task Closure_Is_Not_Allocated_When_Instance_Or_Static_Fields_Were_Used()
        {
            await ValidateCodeAsync(@"
class A {
    private static int s = 42;
    private int i = 42;
    public void M(int y)
    {
        // Delegate allocation, but no closure allocations.
        System.Func<int> f = () => s + i;
        f();
    }
}");
        }

        [Test]
        public async Task Closure_Is_Allocated_In_The_Beginning_Event_If_Used_In_Nested_If_Block()
        {
            await ValidateCodeAsync(@"
class A {
    public void M(int y)
    [|{|]
        // Closure allocation
        if (y > 0)
        {
            // Closure allocation
            System.Func<int> f = () => y;
            f();
        }
    }
}");
        }

        [Test]
        public async Task Closures_Are_Allocated_In_Two_Scopes()
        {
            await ValidateCodeAsync(@"
class A {
    public void F(int a)
    [|{|]
        // The first closure is allocated here
        if (a > 10)
        [|{|]
            // And the second closure is allocated here
            int k = 42;
            System.Func<int> f2 = () => a + k;
            f2();
        }
    }
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
            	System.Func<int> f2 = () => scope1 + scope2 + scope3;
            	f2();
        	}
        }
    }
}");
        }

        [Test]
        public async Task Closure_Is_Allocated_In_ForEach_Loop_No_Block()
        {
            await ValidateCodeAsync(@"
using System;
using System.Collections.Generic;
class A {
    private void Foo(int[] numbers)
    [|{|]
        int x = 42;
        List<Action> actions = new List<Action>();
        [|foreach|](var n in numbers)
            actions.Add(() => Console.WriteLine(x + n));
    }
}");
        }

        [Test]
        public async Task Closure_Is_Allocated_In_ForEach_Loop_Block()
        {
            await ValidateCodeAsync(@"
using System;
using System.Collections.Generic;
class A {
    private void Foo(int[] numbers)
    [|{|]
        int x = 42;
        List<Action> actions = new List<Action>();
        foreach(var n in numbers)
        [|{|]
            actions.Add(() => Console.WriteLine(x + n));
        }
    }
}");
        }

        [Test]
        public async Task Closure_Is_Allocated_In_For_Loop()
        {
            await ValidateCodeAsync(@"
using System;
using System.Collections.Generic;
class A {
    private void Foo()
    [|{|]
        List<Action> actions = new List<Action>();
        for(int n = 12; n < 5; n+=2)
        [|{|]
            int k = n;
            actions.Add(() => Console.WriteLine(n + k));
        }
    }
}");
        }
    }
}
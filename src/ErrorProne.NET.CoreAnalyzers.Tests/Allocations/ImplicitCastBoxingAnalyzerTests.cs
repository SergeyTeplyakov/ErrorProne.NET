using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.Allocations.ImplicitCastBoxingAllocationAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class ImplicitCastBoxingAnalyzerTests
    {
        [Test]
        public async Task Implicit_Conversion_In_String_Construction_Causes_Boxing()
        {
            string code = @"
using System;
using System.Collections.Generic;
using System.Linq;
struct S {}
static class A {
	static void Main()
    {
        int n = 42;

        string s = string.Empty + [|n|];

        string s2 = $""{[|n|]}"";
    }
}";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task Implicit_Conversion_Causes_Boxing()
        {
            string code = @"
using System;
using System.Collections.Generic;
using System.Linq;
struct S {}
static class A {
	static void Main()
    {
        object o = [|42|];
        o = [|52|];
        IConvertible c = [|42|];
        c = [|52|];

        // argument conversion
        foo([|42|]);
        void foo(object arg) {}

        // return statement conversion
        object bar(int n) => [|n|];
    }
}";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task Implicit_Conversion_In_Params_Causes_Boxing()
        {
            string code = @"
static class A {
    static void Bar(params object[] p)
    {
    }
	static void Main()
    {
        Bar([|42|]);
    }
}";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task Implicit_Boxing_For_Delegate_Construction_From_Struct()
        {
            string code = @"
using System;
public class C {
 	public void M() {}   
}

public struct S {
    public void M() {
    }
    
    public static void StaticM() {}
    
    public static void Run()
    {
        C c = new C();
        S s = new S();
        
        // c is not boxed!
        Action a = c.M;
        a();
        
        // s is boxed
        a = new Action([|s|].M);
        a();

        // nothing to box
        a = new Action(S.StaticM);
        a();
    }
}";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task Yield_Return_Int_Causes_Boxing()
        {
            string code = @"
using System;
using System.Collections.Generic;
using System.Linq;
struct S {}
static class A {
	static IEnumerable<object> Foo()
    {
        yield return [|42|];
    }
}";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task Test_Implicit_Boxing_When_Foreach_Upcasts_Int_To_Object()
        {

            string code = @"
using System;
using System.Collections.Generic;
using System.Linq;
struct S {}
static class A {
	static void Main()
    {
        foreach(object obj in [|Enumerable.Range(1,10)|]) {}
        foreach(object obj in [|new int[0]|]) {}
    }
}";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
    }
}
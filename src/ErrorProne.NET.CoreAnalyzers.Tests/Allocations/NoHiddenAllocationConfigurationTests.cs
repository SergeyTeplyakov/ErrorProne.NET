using ErrorProne.NET.AsyncAnalyzers;
using ErrorProne.NET.CoreAnalyzers.Allocations;
using JetBrains.dotMemoryUnit;
using JetBrains.dotMemoryUnit.Kernel;
using NUnit.Framework;

namespace ErrorProne.NET.CoreAnalyzers.Tests.Allocations
{
    [TestFixture]
    public class NoHiddenAllocationConfigurationTests
    {
        static void VerifyCode(string code) => AllocationTestHelper.VerifyCodeWithoutAssemblyAttributeInjection<ImplicitCastBoxingAllocationAnalyzer>(code);

        [Test]
        public void Functions()
        {
            VerifyCode(@"
[NoHiddenAllocations]
class A {
    static object F() => [|1|];
    static object G() {
        return [|1|];
    }
}");

        }
        
        [Test]
        public void Local_Function()
        {
            VerifyCode(@"
class A {
    [NoHiddenAllocations]	
    static object F() {
        return $""{local()} {curlyLocal()}"";

        object local() => [|1|];

        object curlyLocal() 
        {
            return [|1|];
        }
    }
}");

        }

        /// <summary>
        /// 
        /// </summary>
        [Test]
        public void Properties()
        {
            VerifyCode(@"
class A {
    
    [NoHiddenAllocations]    
    public object B => [|1|];

    [NoHiddenAllocations]
    public object C {
        get => [|1|];
    }

    [NoHiddenAllocations]
    public object D {
        get { return [|1|];}
    }

    public object E {
        [NoHiddenAllocations]
        get => [|1|];

        [NoHiddenAllocations]
        set {
            object o = [|1|];
        }
    }
    
    [NoHiddenAllocations]
    public object F {
        get { return [|1|]; }
    }

    [NoHiddenAllocations]
    public object G {
        get { 
            return local();

            object local() => [|1|];
        }
    }
}");

        }
        
        [Test]
        public void Partial_Class()
        {
            VerifyCode(@"
[NoHiddenAllocations]
partial class A { }

partial class A {
    
    static object F() {
        
        return [|1|];
    }
}");
        }
    }
}
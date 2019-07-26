using System.Collections.Generic;
using ErrorProne.NET.AsyncAnalyzers;
using ErrorProne.NET.CoreAnalyzers.Allocations;
using JetBrains.dotMemoryUnit;
using JetBrains.dotMemoryUnit.Kernel;
using NUnit.Framework;

namespace ErrorProne.NET.CoreAnalyzers.Tests.Allocations
{
    public static class StringExtensions
    {
        public static string ReplaceAttribute(this string codeSnippet, string actualAttribute)
        {
            return codeSnippet.Replace("[NoHiddenAllocations]", actualAttribute);
        }
    }

    [TestFixture]
    public class NoHiddenAllocationConfigurationTests
    {
        private static void VerifyCode(string code)
        {
            AllocationTestHelper.VerifyCode<ImplicitCastBoxingAllocationAnalyzer>(code, injectAssemblyLevelConfigurationAttribute: false);
        }

        private static object[] NoHiddenAllocationAttributeCombinations =
        {
            new object[] {"[NoHiddenAllocations]"},
            new object[] {"[NoHiddenAllocations(Recursive = true)]"},
            new object[] {"[NoHiddenAllocations(Recursive = false)]"},
            new object[] {"[NoHiddenAllocations(Recursive = false || true)]"}
        };

        [TestCaseSource(nameof(NoHiddenAllocationAttributeCombinations))]
        public void Functions(string noHiddenAllocationAttribute)
        {
            VerifyCode(@"
[NoHiddenAllocations]
class A {
    static object F() => [|1|];
    static object G() {
        return [|1|];
    }
}".ReplaceAttribute(noHiddenAllocationAttribute));
        }

        [TestCaseSource(nameof(NoHiddenAllocationAttributeCombinations))]
        public void Local_Function(string noHiddenAllocationAttribute)
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
}".ReplaceAttribute(noHiddenAllocationAttribute));
        }

        [TestCaseSource(nameof(NoHiddenAllocationAttributeCombinations))]
        public void Properties(string noHiddenAllocationAttribute)
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
}".ReplaceAttribute(noHiddenAllocationAttribute));
        }

        [TestCaseSource(nameof(NoHiddenAllocationAttributeCombinations))]
        public void Partial_Class(string noHiddenAllocationAttribute)
        {
            VerifyCode(@"
[NoHiddenAllocations]
partial class A { }

partial class A {
    
    static object F() {
        
        return [|1|];
    }
}".ReplaceAttribute(noHiddenAllocationAttribute));
        }
    }
}

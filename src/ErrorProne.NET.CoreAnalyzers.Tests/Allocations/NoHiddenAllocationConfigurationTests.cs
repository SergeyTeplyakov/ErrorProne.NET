using System.Collections.Generic;
using System.Threading.Tasks;
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
        private static void VerifyAllocatingCode(string code)
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
            VerifyAllocatingCode(@"
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
            VerifyAllocatingCode(@"
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
            VerifyAllocatingCode(@"
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
            VerifyAllocatingCode(@"
[NoHiddenAllocations]
partial class A { }

partial class A {
    
    static object F() {
        
        return [|1|];
    }
}".ReplaceAttribute(noHiddenAllocationAttribute));
        }

        [TestCaseSource(nameof(NoHiddenAllocationAttributeCombinations))]
        public async Task Recursive_Application_Is_Enforced(string noHiddenAllocationAttribute)
        {
            await AllocationTestHelper.VerifyCodeAsync<RecursiveNoHiddenAllocationAttributeAnalyzer>(@"
static class DirectCallsiteClass {

    [NoHiddenAllocations(Recursive = true)]
    static void DirectCallsiteMethod() {
        IndirectTargetClass.IndirectTargetMethod();
        
        DirectTargetClass.DirectTargetMethod();
        [|DirectTargetClass.NonMarkedMethod()|];

        [|DirectTargetWithoutReceiver()|];
    }
    
    static void DirectTargetWithoutReceiver(){
    }
}

[NoHiddenAllocations(Recursive = true)]
static class IndirectCallsiteClass {
    static void IndirectCallsiteMethod() {
        IndirectTargetClass.IndirectTargetMethod();
        
        DirectTargetClass.DirectTargetMethod();
        [|DirectTargetClass.NonMarkedMethod()|];

        IndirectTargetWithoutReceiver();
    }

    static void IndirectTargetWithoutReceiver(){
    }
}

[NoHiddenAllocations]
class IndirectTargetClass {
    
    public static void IndirectTargetMethod() {
    }
}

class DirectTargetClass {
    
    [NoHiddenAllocations]
    public static void DirectTargetMethod() {
    }

    public static void NonMarkedMethod() {
    }
}
".ReplaceAttribute(noHiddenAllocationAttribute), injectAssemblyLevelConfigurationAttribute: false);
        }

        [TestCaseSource(nameof(NoHiddenAllocationAttributeCombinations))]
        public async Task Recursive_Application_Callchains(string noHiddenAllocationAttribute)
        {
            await AllocationTestHelper.VerifyCodeAsync<RecursiveNoHiddenAllocationAttributeAnalyzer>(@"
class A {
    [NoHiddenAllocations(Recursive=true)]
    static void B(){
        var a = new A();
        [|a.C().D()|].E();
    }

    [NoHiddenAllocations]
    A C(){
        return this;
    }

    A D(){
        return this;
    }
    
    [NoHiddenAllocations]
    A E(){
        return this;
    }
}
".ReplaceAttribute(noHiddenAllocationAttribute), injectAssemblyLevelConfigurationAttribute: false);
        }

        [Test]
        public async Task Recursive_Application_Properties()
        {
            await AllocationTestHelper.VerifyCodeAsync<RecursiveNoHiddenAllocationAttributeAnalyzer>(@"
class A {    
    public object B => 1;

    public object C {
        get => 1;
    }

    public object D {
        get { return 1;}
    }

    public object E {
        get => 1;

        set {
            object o = 1;
        }
    }
    
    public object F {
        get { return 1; }
    }

    public object G {
        get { 
            return local();

            object local() => 1;
        }
    }
    
    [NoHiddenAllocations(Recursive=true)]
    static void Method(){
        var a = new A();
        
        object o = [|a.B|];
        o = [|a.C|];
        o = [|a.D|];
        o = [|a.E|];

        [|a.E|] = 2;

        o = [|a.F|];
        o = [|a.G|];
    }
}
", injectAssemblyLevelConfigurationAttribute: false);
        }
        
        [Test]
        public async Task Recursive_Application_Is_Not_Sensitive_To_Property_Access_Type()
        {
            await AllocationTestHelper.VerifyCodeAsync<RecursiveNoHiddenAllocationAttributeAnalyzer>(@"
class A {    
    public object B {
        [NoHiddenAllocations]
        get => 1;

        set {
            object o = 1;
        }
    }

    public object C {
        get => 1;
        
        [NoHiddenAllocations]
        set {
            object o = 1;
        }
    }
    
    [NoHiddenAllocations]
    public object D {
        get => 1;

        set {
            object o = 1;
        }
    }
    
    [NoHiddenAllocations(Recursive=true)]
    static void Method(){
        var a = new A();
        
        object o = [|a.B|];
        
        [|a.C|] = 1;

        o = a.D;
        a.D = 2;
    }
}
", injectAssemblyLevelConfigurationAttribute: false);
        }

        [TestCaseSource(nameof(NoHiddenAllocationAttributeCombinations))]
        public async Task Recursive_Application_Is_Sensitive_To_Constructors(string noHiddenAllocationAttribute)
        {
            await AllocationTestHelper.VerifyCodeAsync<RecursiveNoHiddenAllocationAttributeAnalyzer>(@"
namespace Foo
{
    class A {
        [NoHiddenAllocations(Recursive=true)]
        static A(){
            StaticC();
            [|StaticD()|];
        }

        [NoHiddenAllocations]
        static void StaticC(){
        }

        static void StaticD(){
        }

        [NoHiddenAllocations(Recursive=true)]
        void E(){
            object a = new B();
            a = new C();
            a = new D();
            a = [|new E()|];
            a = new E(1);
            a = new F();
            a = [|new G()|];
        }
    }

    [NoHiddenAllocations]
    class B{
    }

    [NoHiddenAllocations]
    class C{
        public C(){
        }
    }

    class D{
        [NoHiddenAllocations]
        public D(){
        }
    }

    class E{
        public E(){
        }
        
        [NoHiddenAllocations]
        public E(int a){
        }
    }

    class F{
    }

    class G{
        public G(){
        }
    }
}
".ReplaceAttribute(noHiddenAllocationAttribute), injectAssemblyLevelConfigurationAttribute: false);
        }
        
        [Test]
        public async Task Recursive_Application_Is_Insensitive_To_Library_Code()
        {
            await AllocationTestHelper.VerifyCodeAsync<RecursiveNoHiddenAllocationAttributeAnalyzer>(@"
using System.Collections.Generic;

namespace Foo
{
    class A {
        [NoHiddenAllocations(Recursive=true)]
        void M(){
            var list = new List<string>();
            list.Add(""test"");

            var count = list.Count;
        }
    }
}
", injectAssemblyLevelConfigurationAttribute: false);
        }
    }
}

using ErrorProne.NET.CoreAnalyzers.Allocations;
using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.Allocations.ImplicitEnumeratorAllocationAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.Allocations
{
    [TestFixture]
    public class ImplicitEnumeratorAllocationAnalyzerTests
    {
        static void VerifyCode(string code) => AllocationTestHelper.VerifyCode<ImplicitEnumeratorAllocationAnalyzer>(code);

        [Test]
        public async Task Foreach_On_Iterator_BLock_Causes_Allocation()
        {
            VerifyCode(@"
using System.Collections.Generic;

class A {
    static IEnumerable<int> Generate()
    {
        yield break;
    }

    static void M()
    {
        foreach(var e in [|Generate()|])
        {
            System.Console.WriteLine(e);
        }
    }
}");
        }
        
        [Test]
        public async Task Foreach_On_Interface_Causes_Boxing()
        {
            VerifyCode(@"
using System.Collections.Generic;

class A {
    static void M(IList<string> list)
    {
        foreach(var e in [|list|])
        {
            System.Console.WriteLine(e);
        }
    }
}");
        }
        
        [Test]
        public async Task Foreach_On_IListExpression_Causes_Boxing()
        {
            VerifyCode(@"
using System.Collections.Generic;

class A {

    static IList<string> GetList() => null;

    static void M()
    {
        foreach(var e in [|GetList()|])
        {
            System.Console.WriteLine(e);
        }
    }
}");
        }
        
        [Test]
        public async Task Foreach_On_Casted_Interface_Causes_Boxing()
        {
            VerifyCode(@"
using System.Collections.Generic;

class A {
    static void M(List<string> list)
    {
        foreach(var e in list)
        {
            System.Console.WriteLine(e);
        }

        foreach(var e in [|(IList<string>)list|])
        {
            System.Console.WriteLine(e);
        }
    }
}");
        }
        


        [Test]
        public async Task Foreach_On_CustomTypeWithStructEnumerator_DoesNotCause_Boxing()
        {
            string code = @"
using System.Collections.Generic;

public struct StructEnumerator {
    public object Current { get; }
    
    public bool MoveNext (){
        return false;
    }
}

public class TheEnumerator{
    public StructEnumerator GetEnumerator(){
        return new StructEnumerator();
    }
}

class A {
    static void M(TheEnumerator list)
    {
        foreach(var e in list)
        {
            System.Console.WriteLine(e);
        }
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
        public async Task Foreach_On_CustomTypeWithClassEnumerator_Causes_Boxing()
        {
            VerifyCode(@"
using System.Collections.Generic;

public class ClassEnumerator {
    public object Current { get; }
    
    public bool MoveNext (){
        return false;
    }
}

public class TheEnumerator{
    public ClassEnumerator GetEnumerator(){
        return new ClassEnumerator();
    }
}

class A {
    static void M(TheEnumerator list)
    {
        foreach(var e in [|list|])
        {
            System.Console.WriteLine(e);
        }
    }
}");
        }
        
        [Test]
        public async Task Foreach_On_String_Causes_No_Boxing()
        {
            string code = @"
using System.Collections.Generic;

class A {
    static void M(string list)
    {
        foreach(var e in list)
        {
            System.Console.WriteLine(e);
        }
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
        public async Task Foreach_On_StringArray_Causes_No_Boxing()
        {
            string code = @"
using System.Collections.Generic;

class A {
    static void M(string[] list)
    {
        foreach(var e in list)
        {
            System.Console.WriteLine(e);
        }
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
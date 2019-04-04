using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Collections.Generic;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.StructAnalyzers.HashTableIncompatibilityAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.StructAnalyzers.Tests
{
    [TestFixture]
    public class HashTableIncompatibilityAnalyzerTests
    {
        [Test]
        public async Task WarnForISetForField()
        {
            string code = @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.ISet<MyStruct>|] hs = null;
}";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        DiagnosticResult.CompilerError("CS0708").WithSpan(4, 55, 4, 57).WithMessage("'Ex.hs': cannot declare instance members in a static class"),
                    },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task WarnForHashSetForReturnType()
        {
            string code = @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.HashSet<MyStruct>|] P() => null;
}";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        DiagnosticResult.CompilerError("CS0708").WithSpan(4, 58, 4, 59).WithMessage("'P': cannot declare instance members in a static class"),
                    },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [TestCaseSource(nameof(GetHasDiagnosticCases))]
        public async Task HasDiagnosticCases(string code)
        {
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        public static IEnumerable<string> GetHasDiagnosticCases()
        {   
            // Warn on System.Tuple
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.ISet<System.Tuple<MyStruct, int>>|] {|CS0708:hs|} = null;
}";
            // Warn on Tuple
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.ISet<(MyStruct, int)>|] {|CS0708:hs|} = null;
}";
            
            // Warn on IDictionary
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.IDictionary<MyStruct, int>|] {|CS0708:hs|} = null;
}";
            
            // Warn on ISet
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.ISet<MyStruct>|] {|CS0708:hs|} = null;
}";
            
            // using
            yield return @"
using C = [|System.Collections.Generic.HashSet<MyStruct>|];
struct MyStruct { }";

            // Base type with interface
            yield return @"
struct MyStruct {}
abstract class Ex : {|CS0246:IBar|} {|CS1003::|} {|CS1721:[|{|CS1003:System|}.Collections.Generic.HashSet<MyStruct>|]|} {
}";
            
            // Base type
            yield return @"
struct MyStruct {}
abstract class Ex : [|System.Collections.Generic.HashSet<MyStruct>|] {
}";
            
            // Implicitely typed local declaration
            yield return @"
struct MyStruct {}
class Ex {
    private void Foo() {[|var|] P = new System.Collections.Generic.HashSet<MyStruct>();}
}";
            
            // Variable declaration
            yield return @"
struct MyStruct {}
class Ex {
    private void Foo() {[|System.Collections.Generic.HashSet<MyStruct>|] P = null;}
}";// Variable declaration
            yield return @"
struct MyStruct {}
class Ex {
    private void Foo() {[|System.Collections.Generic.HashSet<MyStruct>|] P = null;}
}";
            
            // Local method
            yield return @"
struct MyStruct {}
class Ex {
    private void Foo() {[|System.Collections.Generic.HashSet<MyStruct>|] P() => null;}
}";

            // Method's return type as array
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.HashSet<MyStruct>[]|] {|CS0708:P|}() => null;
}";
            
            // Method's return type
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.HashSet<MyStruct>|] {|CS0708:P|}() => null;
}";
            // Method's parameter
            yield return @"
struct MyStruct {}
static class Ex {
    private void {|CS0708:Foo|}([|System.Collections.Generic.HashSet<MyStruct>|] arg) {}
}";
            // Property
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.HashSet<MyStruct>|] {|CS0708:P|} {get;}
}";
            
            // Property with getter and setter
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.HashSet<MyStruct>|] {|CS0708:P|} {get; private {|CS0273:set|};}
}";
            
            // Property with getter only
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.HashSet<MyStruct>|] {|CS0708:P|} {get => null;}
}";

            // ImmutableDictionary
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Immutable.ImmutableDictionary<MyStruct, int>|] {|CS0708:hs|} = null;
}";
            
            // ImmutableHashSet
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Immutable.ImmutableHashSet<MyStruct>|] {|CS0708:hs|} = null;
}";
            
            // ConcurrentDictionary
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Concurrent.ConcurrentDictionary<MyStruct, int>|] {|CS0708:hs|} = null;
}";
            
            // Dictionary
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.Dictionary<MyStruct, int>|] {|CS0708:hs|} = null;
}";
            
            // Only GetHashCode
            yield return @"
struct MyStruct {public override int GetHashCode() => 42;}
static class Ex {
    private [|System.Collections.Generic.HashSet<MyStruct>|] {|CS0708:hs|} = null;
}";
            
            // Only Equals
            yield return @"
struct MyStruct {public override bool Equals(object other) => true;}
static class Ex {
    private [|System.Collections.Generic.HashSet<MyStruct>|] {|CS0708:hs|} = null;
}";
        }
        
        [TestCaseSource(nameof(GetNoDiagnosticCases))]
        public async Task NoDiagnosticCases(string code)
        {
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        public static IEnumerable<string> GetNoDiagnosticCases()
        {
            yield return @"
struct MyStruct {}
static class Ex {
    public static void Foo<T>() {}
    private static void Bar()
    {
        Foo<System.Collections.Generic.IDictionary<MyStruct, int>>();
    }
}";
            // No diagnostic if both members are overriden
            yield return @"
struct MyStruct { public override int GetHashCode() => 42; public override bool Equals(object other) => true;}
static class Ex {
    private System.Collections.Generic.HashSet<MyStruct> {|CS0708:hs|} = null;
}";

            // No diagnostic for class
            yield return @"
class MyClass { }
static class Ex {
    private System.Collections.Generic.HashSet<MyClass> {|CS0708:hs|} = null;
}";

            // No diagnostic for enum
            yield return @"
enum MyEnum { value1 = 42, value2 }
static class Ex {
    private System.Collections.Generic.HashSet<MyEnum> {|CS0708:hs|} = null;
}";

            // No diagnostic for interfaces
            yield return @"
interface I { }
static class Ex {
    private System.Collections.Generic.HashSet<I> {|CS0708:hs|} = null;
}";

            // No diagnostic for second type parameter
            yield return @"
struct MyStruct {}
static class Ex {
    private System.Collections.Generic.Dictionary<int, MyStruct> {|CS0708:hs|} = null;
}";
            
            // No diagnostic for second type parameter
            yield return @"
static class Ex {
    private System.Collections.Generic.ISet<T> {|CS0708:GetSet|}<T>() => null;
}";
            
            // No diagnostic for T with constraint
            yield return @"
static class Ex {
    private System.Collections.Generic.ISet<T> {|CS0708:GetSet|}<T>() where T : struct => null;
}";
        }
    }
}
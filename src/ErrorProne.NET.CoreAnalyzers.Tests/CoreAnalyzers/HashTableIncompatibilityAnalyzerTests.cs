using System.Collections.Generic;
using System.Threading.Tasks;
using ErrorProne.NET.TestHelpers;
using NUnit.Framework;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.HashTableIncompatibilityAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.CoreAnalyzers
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
    private static [|System.Collections.Generic.ISet<MyStruct>|] hs = null;
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
        public async Task WarnForHashSetForReturnType()
        {
            string code = @"
struct MyStruct {}
static class Ex {
    private static [|System.Collections.Generic.HashSet<MyStruct>|] P() => null;
}";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
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
    private static [|System.Collections.Generic.ISet<System.Tuple<MyStruct, int>>|] hs = null;
}";
            // Warn on Tuple
            yield return @"
struct MyStruct {}
static class Ex {
    private static [|System.Collections.Generic.ISet<(MyStruct, int)>|] hs = null;
}";
            
            // Warn on IDictionary
            yield return @"
struct MyStruct {}
static class Ex {
    private static [|System.Collections.Generic.IDictionary<MyStruct, int>|] hs = null;
}";
            
            // Warn on ISet
            yield return @"
struct MyStruct {}
static class Ex {
    private static [|System.Collections.Generic.ISet<MyStruct>|] hs = null;
}";
            
            // using
            yield return @"
using C = [|System.Collections.Generic.HashSet<MyStruct>|];
struct MyStruct { }";

            // Base type with interface
            yield return @"
struct MyStruct {}
public interface IBar {}
abstract class Ex : [|System.Collections.Generic.HashSet<MyStruct>|], IBar {
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
    private static [|System.Collections.Generic.HashSet<MyStruct>[]|] P() => null;
}";
            
            // Method's return type
            yield return @"
struct MyStruct {}
static class Ex {
    private static [|System.Collections.Generic.HashSet<MyStruct>|] P() => null;
}";
            // Method's parameter
            yield return @"
struct MyStruct {}
static class Ex {
    private static void Foo([|System.Collections.Generic.HashSet<MyStruct>|] arg) {}
}";
            // Property
            yield return @"
struct MyStruct {}
static class Ex {
    private static [|System.Collections.Generic.HashSet<MyStruct>|] P {get;}
}";
            
            // Property with getter and setter
            yield return @"
struct MyStruct {}
static class Ex {
    private static [|System.Collections.Generic.HashSet<MyStruct>|] P {get; set;}
}";
            
            // Property with getter only
            yield return @"
struct MyStruct {}
static class Ex {
    private static [|System.Collections.Generic.HashSet<MyStruct>|] P {get => null;}
}";

            // ImmutableDictionary
            yield return @"
struct MyStruct {}
static class Ex {
    private static [|System.Collections.Immutable.ImmutableDictionary<MyStruct, int>|] hs = null;
}";
            
            // ImmutableHashSet
            yield return @"
struct MyStruct {}
static class Ex {
    private static [|System.Collections.Immutable.ImmutableHashSet<MyStruct>|] hs = null;
}";
            
            // ConcurrentDictionary
            yield return @"
struct MyStruct {}
static class Ex {
    private static [|System.Collections.Concurrent.ConcurrentDictionary<MyStruct, int>|] hs = null;
}";
            
            // Dictionary
            yield return @"
struct MyStruct {}
static class Ex {
    private static [|System.Collections.Generic.Dictionary<MyStruct, int>|] hs = null;
}";
            
            // Only GetHashCode
            yield return @"
struct MyStruct {public override int GetHashCode() => 42;}
static class Ex {
    private static [|System.Collections.Generic.HashSet<MyStruct>|] hs = null;
}";
            
            // Only Equals
            yield return @"
struct MyStruct {public override bool Equals(object other) => true;}
static class Ex {
    private static [|System.Collections.Generic.HashSet<MyStruct>|] hs = null;
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
    private static System.Collections.Generic.HashSet<MyStruct> hs = null;
}";

            // No diagnostic for class
            yield return @"
class MyClass { }
static class Ex {
    private static System.Collections.Generic.HashSet<MyClass> hs = null;
}";

            // No diagnostic for enum
            yield return @"
enum MyEnum { value1 = 42, value2 }
static class Ex {
    private static System.Collections.Generic.HashSet<MyEnum> hs = null;
}";

            // No diagnostic for interfaces
            yield return @"
interface I { }
static class Ex {
    private static System.Collections.Generic.HashSet<I> hs = null;
}";

            // No diagnostic for second type parameter
            yield return @"
struct MyStruct {}
static class Ex {
    private static System.Collections.Generic.Dictionary<int, MyStruct> hs = null;
}";
            
            // No diagnostic for second type parameter
            yield return @"
static class Ex {
    private static System.Collections.Generic.ISet<T> GetSet<T>() => null;
}";
            
            // No diagnostic for T with constraint
            yield return @"
static class Ex {
    private static System.Collections.Generic.ISet<T> GetSet<T>() where T : struct => null;
}";
        }
    }
}
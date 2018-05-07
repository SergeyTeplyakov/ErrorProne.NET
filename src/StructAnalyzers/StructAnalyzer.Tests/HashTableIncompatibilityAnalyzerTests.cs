using System.Collections.Generic;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Structs.Tests
{
    [TestFixture]
    public class HashTableIncompatibilityAnalyzerTests : CSharpAnalyzerTestFixture<HashTableIncompatibilityAnalyzer>
    {
        public const string DiagnosticId = HashTableIncompatibilityAnalyzer.DiagnosticId;

        [Test]
        public void WarnForISetForField()
        {
            string code = @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.ISet<MyStruct>|] hs = null;
}";
            HasDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void WarnForHashSetForReturnType()
        {
            string code = @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.HashSet<MyStruct>|] P() => null;
}";
            HasDiagnostic(code, DiagnosticId);
        }
        
        [TestCaseSource(nameof(GetHasDiagnosticCases))]
        public void HasDiagnosticCases(string code)
        {
            HasDiagnostic(code, DiagnosticId);
        }

        public static IEnumerable<string> GetHasDiagnosticCases()
        {   
            // Warn on System.Tuple
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.ISet<System.Tuple<MyStruct, int>>|] hs = null;
}";
            // Warn on Tuple
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.ISet<(MyStruct, int)>|] hs = null;
}";
            
            // Warn on IDictionary
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.IDictionary<MyStruct, int>|] hs = null;
}";
            
            // Warn on ISet
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.ISet<MyStruct>|] hs = null;
}";
            
            // using
            yield return @"
using C = [|System.Collections.Generic.HashSet<MyStruct>|];";

            // Base type with interface
            yield return @"
struct MyStruct {}
abstract class Ex : IBar : [|System.Collections.Generic.HashSet<MyStruct>|] {
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
    private [|System.Collections.Generic.HashSet<MyStruct>[]|] P() => null;
}";
            
            // Method's return type
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.HashSet<MyStruct>|] P() => null;
}";
            // Method's parameter
            yield return @"
struct MyStruct {}
static class Ex {
    private void Foo([|System.Collections.Generic.HashSet<MyStruct>|] arg) {}
}";
            // Property
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.HashSet<MyStruct>|] P {get;}
}";
            
            // Property with getter and setter
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.HashSet<MyStruct>|] P {get; private set;}
}";
            
            // Property with getter only
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.HashSet<MyStruct>|] P {get => null;}
}";

            // ImmutableDictionary
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Immutable.ImmutableDictionary<MyStruct, int>|] hs = null;
}";
            
            // ImmutableHashSet
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Immutable.ImmutableHashSet<MyStruct>|] hs = null;
}";
            
            // ConcurrentDictionary
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Concurrent.ConcurrentDictionary<MyStruct, int>|] hs = null;
}";
            
            // Dictionary
            yield return @"
struct MyStruct {}
static class Ex {
    private [|System.Collections.Generic.Dictionary<MyStruct, int>|] hs = null;
}";
            
            // Only GetHashCode
            yield return @"
struct MyStruct {public override int GetHashCode() => 42;}
static class Ex {
    private [|System.Collections.Generic.HashSet<MyStruct>|] hs = null;
}";
            
            // Only Equals
            yield return @"
struct MyStruct {public override bool Equals(object other) => true;}
static class Ex {
    private [|System.Collections.Generic.HashSet<MyStruct>|] hs = null;
}";
        }
        
        [TestCaseSource(nameof(GetNoDiagnosticCases))]
        public void NoDiagnosticCases(string code)
        {
            NoDiagnostic(code, DiagnosticId);
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
    private System.Collections.Generic.HashSet<MyStruct> hs = null;
}";
            
            // No diagnostic for second type parameter
            yield return @"
struct MyStruct {}
static class Ex {
    private System.Collections.Generic.Dictionary<int, MyStruct> hs = null;
}";
        }
    }
}
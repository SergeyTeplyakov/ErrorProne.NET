using ErrorProne.NET.TestHelpers;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.StructAnalyzers.HiddenStructCopyAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.StructAnalyzers.Tests
{
    [TestFixture]
    public class HiddenStructCopyAnalyzerTests
    {
        [Test]
        public async Task NoWarnOnDispose()
        {
            // Dispose method is special: indeed it causes a hidden copy,
            // but its kind of by design, and it can't be called to often.
            // So Dispose calls should not cause warnings.
            string code = @"
struct Struct : System.IDisposable
{
 private readonly long l1, l2, l3;

 public void Dispose() {} 
}
class Test
{
  private readonly Struct _s;
  void Foo() {
    _s.Dispose();
  }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        // [Test]
        public async Task NoWarnOnPropertyGetter()
        {
            string code = @"
struct Struct1
{
 private readonly long l1, l2, l3;
}

struct Struct2
{
  private readonly long l1, l2, l3;
  public Struct1 S1 {get;}
}
class Class {
  public readonly Struct2 S2;
}
class Test
{
  void Foo() {
    Class s3 = default;
    var s = s3?.S2.S1.ToString();
  }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }
        
        [Test]
        public async Task WarnOnDisposeNotFromIDisposable()
        {
            string code = @"
struct Struct
{
 private readonly long l1, l2, l3;

 public void Dispose() {} 
}
class Test
{
  private readonly Struct _s;
  void Foo() {
    _s.[|Dispose|]();
  }
}";
            await VerifyCS.VerifyAsync(code);
        }
        
        // [Test]
        public async Task WarOnMethodCall()
        {
            string code = @"
public struct TestStruct
    {
        private int _value;

        public int GetValue() => _value;
    }

    public static class Program
    {
        public static int GetValue(in TestStruct test)
        {
            return test.[|GetValue()|];
        }
    }";
            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task NoWarningsOnExtensionMethods()
        {
            string code = @"
struct Struct
{
 public int X;
}
static class StructExtensions
{
  public static int Squared(in this Struct s) => s.X * s.X;
}
class Test
{
  void doSomething(in Struct s)
  {
    s.Squared(); // <- no hidden defensive copy happens, since Squared is an in-extension method only accessing fields readonly, which can not modify the struct
  }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task NoNullReferenceExceptionWhenExtensionMethodIsCalledAsAMethod()
        {
            string code = @"
static class Ex {
    public static void Foo(this object o) {}
    public static void Example(object o) => Ex.Foo(o);
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }
        
        [Test]
        public async Task NoWarningsForAccessingNullableProperties()
        {
            string code = @"
class Foo {
    private readonly (string s, string y)? _tuple;
    private void Test() {
        var b = _tuple.HasValue; // No hidden copies
        var s = _tuple.Value.s; // No hidden copies, this is an explicit copy!
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }
        
        [Test]
        public async Task HasDiagnosticsForMethodCallsOnReadOnlyField()
        {
            string code = @"struct S {private readonly long l1,l2,l3; public int Foo() => 42;} class Foo {private readonly S _s; public string Bar() => _s.[|Foo|]().ToString();}";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task HasDiagnosticsForMethodCallsOnInParameter()
        {
            string code = @"struct S {private readonly long l1,l2,l3; public int Foo() => 42;} class Foo {public int Bar(in S s) => s.[|Foo|]();}";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task HasDiagnosticsForMethodCallsOnRefReadOnly()
        {
            string code = @"
struct S { private readonly long l1,l2,l3; public int Foo() => 42; }
class Foo {
    public int Bar(S s)
    {
        ref readonly var rs = ref s;
        return rs.[|Foo|]();
    }
}";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task HasDiagnosticsForIndexerOnReadOnlyField()
        {
            string code = @"
struct S {
    private readonly long l1,l2,l3; 
    public string this[int x] => string.Empty;
}
public class C {
    private readonly S _s;
    public string M() => [|_s|][0];
}"; ;
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task HasDiagnosticsWhenExtensionMethodIsUsedThatTakesStructByValue()
        {
            string code = @"
readonly struct S {
    private readonly long l1,l2,l3; 
    public static void Sample() {
       S s = default(S);
       s.[|Foo|]();
    }
}
static class C {
    public static void Foo(this S s) {}
}"; ;
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task NoDiagnosticsWhenExtensionMethodIsUsedThatTakesStructByIn()
        {
            string code = @"
public readonly struct S {
    public static void Sample() {
       S s = default(S);
       s.Foo();
    }
}
public static class C {
    public static void Foo(this in S s) {}
}"; ;
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task NoDiagnosticsForNullableTypes()
        {
            string code = @"
public struct Large {private long l1, l2;}
public class C {
   private readonly Large? s;  
   public void Foo() {
       if (s.HasValue) System.Console.WriteLine(s.Value);
   }
}"; ;
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task NoDiagnosticsFieldOfReferenceType()
        {
            string code = @"
class S {
    public string this[int x] => string.Empty;
}
public class C {
    private readonly S _s;
    public string M() => _s[0];
}"; ;
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task NoDiagnosticsFieldOfInParameter()
        {
            string code = @"
struct S {
    public int X;
    public int Foo(in S other) => other.X;
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task NoDiagnosticsFielUsedFromReadOnlyReference()
        {
            string code = @"
struct S {
    public int X;
    public int Foo(S other)
    {
        ref readonly var otherRef = ref other;
        return otherRef.X;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task NoDiagnosticsFieldOfEnumType()
        {
            string code = @"
enum S {X};
static class SEx {
   public static string Get(this S s) => s.ToString();
}
class Foo {private readonly S _s; public string Bar() => _s.Get();}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task NoDiagnosticsWhenFieldIsReferencedDeeperToCallAMethod()
        {
            string code = @"
struct S {public int X; }
class Foo {private readonly S _s; public string Bar() => _s.ToString();}";
            await VerifyCS.VerifyAnalyzerAsync(code);
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
            // On extension method that takes 'this' by value
            yield return @"
readonly struct S { private readonly long l1,l2,l3; public int X => 42;}
static class SEx {
    public static int GetX(this S s) => s.X;

    static int Example(S s) => s.[|GetX|]();
}";

            // On for properties
            yield return "struct S { private readonly long l1,l2,l3; public int X => 42;} class Foo {private readonly S _s; public int Bar() => _s.[|X|];}";

            // On composite dotted expression like a.b.c.ToString();
            yield return "struct S { private readonly long l1,l2,l3; public int X => 42;} class Foo {private readonly S _s; public string Bar() => _s.[|X|].ToString();}";

            // On for methods
            yield return "struct S {private readonly long l1,l2,l3; public int X() => 42;} class Foo {private readonly S _s; public int Bar() => _s.[|X|]();}";

            // On for indexers
            yield return @"
struct S {
    private readonly long l1,l2,l3; 
    public string this[int x] => string.Empty;
}
public class C {
    private readonly S _s;
    public string M() => [|_s|][0];
}";
            
            // On readonly ref returns
            yield return @"
struct S {
    private readonly long l1,l2,l3; 
}
public class C {
    private readonly S _s;
    private ref readonly S GetS() => ref _s;
    private void Test() {
       string s = GetS().[|ToString|]();
    }
}";
        }
        
        [TestCaseSource(nameof(GetNoDiagnosticCases))]
        public async Task NoDiagnosticCases(string code)
        {
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        public static IEnumerable<string> GetNoDiagnosticCases()
        {
            // No diagnostics for a small struct
            yield return @"
readonly struct S { private readonly long l1; public int X => 42;}
static class SEx {
    public static int GetX(this S s) => s.X;

    static int Example(S s) => s.GetX();
}";

            // No diagnostics if 'this' is passed by 'in'
            yield return @"
readonly struct S { private readonly long l1,l2,l3; public int X => 42;}
static class SEx {
    public static int GetX(in this S s) => s.X;

    static int Example(S s) => s.GetX();
}";

            // No diagnostics if a struct small
            yield return "struct S {private readonly int _n; public override int GetHashCode() => _n.GetHashCode();}";
            
            // No diagnostics for CancellationToken
            yield return "struct S {private readonly System.Threading.CancellationToken _n; public override int GetHashCode() => _n.GetHashCode();}";
            
            // No diagnostics if a struct is readonly
            yield return "readonly struct S {public int X => 42;} class Foo {private readonly S _s; public int Bar() => _s.X; }";

            // No diagnostics if field accessed
            yield return "struct S {public int X;} class Foo {private readonly S _s; public int Bar() => _s.X;}";

            // No diagnostics for enum
            yield return "enum S {X}; class Foo {private readonly S _s; public int Bar() => (int)_s;}";
        }
    }
}
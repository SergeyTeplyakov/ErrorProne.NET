using System;
using System.Collections.Generic;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.StructAnalyzers.MakeStructMemberReadOnlyAnalyzer,
    ErrorProne.NET.StructAnalyzers.MakeStructMemberReadOnlyCodeFixProvider>;

namespace ErrorProne.NET.StructAnalyzers.Tests
{
    [TestFixture]
    public class MakeStructMemberReadOnlyAnalyzerTests
    {
        [SetUp]
        public void Initialize()
        {
            Settings.SetDefaultLargeStructThreshold(0);
        }

        [TearDown]
        public void TearDown()
        {
            Settings.SetDefaultLargeStructThreshold(Settings.DefaultLargeStructThreshold);
        }

        [Test]
        public async Task SimpleReadOnlyProperty()
        {
            string code = @"struct Test {
    private int x; // Need to have a non-readonly field because otherwise the entire struct can be readonly
    public int [|X|] => 42;
}";

            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task NoWarnIfTheEntireStructCanBeReadOnly()
        {
            string code = @"struct Test {
    public readonly int Field;
    public int X => 42;
}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }
        
        [Test]
        public async Task WarnOnRefReadonly()
        {
            string code = @"
struct SelfAssign
{
    public int Field;
    public SelfAssign(int f) => Field = f;

    public void [|Foo|]()
    {
        ref readonly SelfAssign x = ref this; // This is fine
    }
}
";

            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task NoWarnOnRef()
        {
            string code = @"
struct SelfAssign
{
    public int Field;
    public SelfAssign(int f) => Field = f;

    public void Foo()
    {
        ref SelfAssign x = ref this; // This is no fine! non-readonly ref!
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }
        
        [Test]
        public async Task NoWarnForSelfAssign()
        {
            string code = @"struct SelfAssign {
    public readonly int Field;
    private int x; // Need to have a non-readonly field because otherwise the entire struct can be readonly
    public void M(SelfAssign other) {
if (other.Field > 0)      
this = other;
    }
}";

            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task WarnWriteToStaticField()
        {
            string code = @"struct Test {
    public static int Field;
    private int x; // Need to have a non-readonly field because otherwise the entire struct can be readonly
    public int [|X|] => Field = 42;
    public int [|Y|] { get => Field = 42;}
    public int [|Z|] {get {Field = 42; return 1;}}
    public int [|K|] {get {Field = 42; return 1;} set {Field = value;}}
}";

            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task NoWarnWriteToStaticField()
        {
            Settings.SetDefaultLargeStructThreshold(42);
            string code = @"struct Test {
    public static int Field;
    private int x; // Need to have a non-readonly field because otherwise the entire struct can be readonly
    public int X => Field = 42;
    public int Y { get => Field = 42;}
    public int Z {get {Field = 42; return 1;}}
    public int K {get {Field = 42; return 1;} set {Field = value;}}
}";

            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task WarnOnAMethodThatReturnsAProperty()
        {
            string code = @"struct Test {
    public int X {get;}
    private int x; // Need to have a non-readonly field because otherwise the entire struct can be readonly
    public int [|GetX|]() => X;    
}";

            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task WriteToInstanceField()
        {
            string code = @"struct Test {
    public int Field;
    public int X => Field = 42;
    public int Y { get => Field = 42;}
    public int Z {get {Field = 42; return 1;}}
    public int K {get {return 1;} set {Field = value;}}
    public int L {get {Field = 42; return 1;} set {}}
}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [TestCaseSource(nameof(WarnForExpressionBodyCases))]
        public async Task WarnForExpressionBody(string code)
        {
            await VerifyCS.VerifyAsync(code);
        }

        public static IEnumerable<string> WarnForExpressionBodyCases()
        {
            // Writes to static field
            yield return @"struct Test {
    private int x; // Need to have a non-readonly field because otherwise the entire struct can be readonly
    public static int Field;
    public int [|X|] => Field = 42;
}";
            
            // Writes to object instance
            yield return @"struct Test {
    public Foo f;
    public int [|X|] => f.X = 42;
}
public class Foo { public int X;}
";
            
            // Reads only
            yield return @"struct Test {
    private int x, y;
    public int [|X|] => x + y;
}
";
        }
        
        [TestCaseSource(nameof(NoDiagnosticsAlreadyReadOnlyCases))]
        public async Task NoDiagnosticsAlreadyReadOnly(string code)
        {
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        public static IEnumerable<string> NoDiagnosticsAlreadyReadOnlyCases()
        {
            // Property is already readonly
            yield return @"struct Test {
    private int x; // Need to have a non-readonly field because otherwise the entire struct can be readonly
    public static int Field;
    public readonly int X => Field = 42;
}";
            
            // Struct is already readonly
            yield return @"readonly struct Test {
    public static int Field;
    public int X => Field = 42;
}";
            
            // Method is already readonly
            yield return @"struct Test {
    private int x; // Need to have a non-readonly field because otherwise the entire struct can be readonly
    public static int Field;
    public readonly int X() => Field = 42;
}";
        }

        [TestCaseSource(nameof(NoDiagnosticsWritesExpressionBody))]
        public async Task NoDiagnosticsExpressionBody(string code)
        {
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        public static IEnumerable<string> NoDiagnosticsWritesExpressionBody()
        {
            // Simple write to the field
            yield return @"struct Test {
    private int x; // Need to have a non-readonly field because otherwise the entire struct can be readonly
    public int Field;
    public int X => Field = 42;
}";
            
            // This assignment
            yield return @"struct Test {
    private int x; // Need to have a non-readonly field because otherwise the entire struct can be readonly
    public void M(Test other) => this = other;
}";
            
            // Write to tuple
            yield return @"struct Test {
    private int x; // Need to have a non-readonly field because otherwise the entire struct can be readonly
    public int y;
    public int X => ((x, y) = (1, 2)).x;
}";
            
            // By Ref
            yield return @"struct Test {
    public int x;
    public int X => ByRef(ref x);
    private static int ByRef(ref int x) => x;
}";
            
            // By Ref this
            yield return @"struct Test {
    public int x;
    public int X => ByRef(ref this);
    private static int ByRef(ref Test x) => x.x;
}";
            
            // Increment
            yield return @"struct Test {
    private int x;
    public int X => x++;
}";
        }


        [Test]
        public async Task NoWarnWhenExpressionBodyWritesToField()
        {
            string code = @"struct Test2 {
    public int Field;
    public int X => Field = 42;
    public int Y { get => Field = 42;}
    public int Z { get  {Field = 42; return 42;} }
    public int K => (this = new Test2()).Field;
}";

            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task NoWarnWhenExpressionBodyWritesToFieldViaTupleSyntax()
        {
            string code = @"struct Test2 {
    public int Field;
	public int K => ((this, Field) = (new Test2(), 42)).Field;
}";

            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task NoWarnWhenMethodWritesToField()
        {
            string code = @"struct Test {
    public int Field;
    public int A() => Field = 42;
    public int C() { Field = 42; return 42;}
    public int D() {this = new Test(); return 42;}
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task WarnWhenMethodWritesToArgumentField()
        {
            string code = @"struct Test {
    public int Field;
    public int [|X|](Test t) => t.Field = 42;
}";

            await VerifyCS.VerifyAsync(code);
        }

    }
}
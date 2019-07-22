using ErrorProne.NET.CoreAnalyzers.Allocations;
using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.Allocations.ImplicitBoxingAllocationAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class ImplicitBoxingAnalyzerTests
    {
        [Test]
        public async Task Test()
        {

            //string str = s.[|ToString|]() + s2.ToString();
            //var type = s.[|GetType|]();
            //var hc = s.[|GetHashCode|]();
            //var e = s.[|Equals|](default);

            string code = @"
struct S {
}

struct S2 { 
    public override string ToString() => string.Empty;
    public override int GetHashCode() => 42;
    public override bool Equals(object other) => true;
}

class A {

    static S GetS() => default;
    static S MyS => default;

	static void Main() {
		S s = default;
        S2 s2 = default;

        var hc2 = s2.GetHashCode();
        var e2 = s2.Equals(default);

        string str2 = GetS().[|ToString|]() + s2.ToString();
        var type2 = GetS().[|GetType|]();
        var hc3 = GetS().[|GetHashCode|]();
        var e3 = MyS.[|Equals|](default);
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
        public async Task HasFlag_Causes_Implicit_Boxing_Allocation()
        {
            string code = @"
[System.Flags]
enum E {
  V1 = 1,
  V2 = 1 << 1,
}


class A {
	static bool TestHasFlags(E e) => e.[|HasFlag|](E.V2);
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
        public async Task Test2()
        {
            // This is a different case
            string code = @"
struct S { 
}

struct S2 { public override string ToString() => string.Empty; }

class A {
	static void Main() {
		S s = default;
        S2 s2 = default;
        // This is a different case!
        string str = $""{s2} {s}"";
	}
}";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(ImplicitBoxingAllocationAnalyzer.Rule),
                    },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

    }
}
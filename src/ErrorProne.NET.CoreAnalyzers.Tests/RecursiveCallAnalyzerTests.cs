using NUnit.Framework;
using System.Threading.Tasks;
using ErrorProne.NET.CoreAnalyzers;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.RecursiveCallAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class RecursiveCallAnalyzerTests
    {
        [Test]
        public async Task WarnsOnUnconditionalRecursiveCall()
        {
            var test = @"
class C {
    void Foo() {
        [|Foo()|];
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task WarnsOnConditionalRecursiveCall()
        {
            var test = @"
class C {
    void Foo(bool b) {
        if (b) [|Foo(b)|];
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarningOnNonRecursiveCall()
        {
            // The call is recursive, but we're not doing cross-procedural analysis.
            var test = @"
class C {
    void Foo() { Bar(); }
    void Bar() { Foo(); }
}
";
            await Verify.VerifyAsync(test);
        }
    }
}

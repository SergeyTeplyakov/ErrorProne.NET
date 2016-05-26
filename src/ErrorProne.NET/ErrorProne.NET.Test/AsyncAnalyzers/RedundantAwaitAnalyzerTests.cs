using ErrorProne.NET.Common;
using ErrorProne.NET.Rules.AsyncAnalyzers;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.AsyncAnalyzers
{
    [TestFixture]
    public class RedundantAwaitAnalyzerTests : CSharpAnalyzerTestFixture<RedundantAwaitAnalyzer>
    {
        [Test]
        public void ShouldWarnOnOneAwaitOnReturn()
        {
            string code =
@"public class CustomType
{
    private static async Task<int> [|Foo|]()
    {
        return await Task.Run(() => 42);
    }
}";

            HasDiagnostic(code, RuleIds.RedundantAwaitRule);
        }

        [Test]
        public void ShouldWarnOnMultipleReturnsInNestedIfsAndAwaitOnReturn()
        {
            string code =
@"public class CustomType
{
    private static async Task<int> [|Foo|](int arg)
    {
        if (arg == 42) return await Task.FromResult(42);

        return await Task.Run(() => 42);
    }
}";

            HasDiagnostic(code, RuleIds.RedundantAwaitRule);
        }

        [Test]
        public void ShouldWarnOnTernaryOperatorOnReturn()
        {
            string code =
@"public class CustomType
{
    private static async Task<int> [|Foo|](int arg)
    {
        return arg == 42 ? await Task.Run(() => 42) : await Task.FromResult(42);
    }
}";

            HasDiagnostic(code, RuleIds.RedundantAwaitRule);
        }

        [Test]
        public void ShouldNotWarnOnNonAsyncMethod()
        {
            string code =
@"public class CustomType
{
    private static Task<int> Foo(int arg)
    {
        if (arg == 42) return Task.FromResult(42);

        return Task.Run(() => 42);
    }
}";

            NoDiagnostic(code, RuleIds.RedundantAwaitRule);
        }

        [Test]
        public void ShouldNotWarnWhenOneReturnIsNotAwaitable()
        {
            string code =
@"public class CustomType
{
    private static async Task<int> Foo(int arg)
    {
        if (arg == 42) return 42;

        return await Task.Run(() => 42);
    }
}";

            NoDiagnostic(code, RuleIds.RedundantAwaitRule);
        }
    }
}
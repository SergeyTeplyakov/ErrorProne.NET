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
@"using System.Threading.Tasks;
public class CustomType
{
    private static async Task<int> [|Foo|]()
    {
        return await Task.Run(() => 42);
    }
}";

            HasDiagnostic(code, RuleIds.RedundantAwaitRule);
        }

        [Test]
        public void ShouldWarnOnExpressionBody()
        {
            string code =
@"using System.Threading.Tasks;
public class CustomType
{
    private static async Task<int> [|Foo|](int arg) => await Task.FromResult(42);
}";

            HasDiagnostic(code, RuleIds.RedundantAwaitRule);
        }

        [Test]
        public void ShouldNotWarnOnExpressionBody()
        {
            string code =
@"using System.Threading.Tasks;
public class CustomType
{
    private static async Task<int> Foo(int arg) => Task.FromResult(42);
}";

            NoDiagnostic(code, RuleIds.RedundantAwaitRule);
        }

        [Test]
        public void ShouldWarnOnMultipleReturnsInNestedIfsAndAwaitOnReturn()
        {
            string code =
@"using System.Threading.Tasks;
public class CustomType
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
@"using System.Threading.Tasks;
public class CustomType
{
    private static async Task<int> [|Foo|](int arg)
    {
        return arg == 42 ? await Task.Run(() => 42) : await Task.FromResult(42);
    }
}";

            HasDiagnostic(code, RuleIds.RedundantAwaitRule);
        }

        [Test]
        public void ShouldWarnOnTernaryOperatorOnReturnWithNestedBraces()
        {
            string code =
@"using System.Threading.Tasks;
public class CustomType
{
    private static async Task<int> [|Foo|](int arg)
    {
        return ((((arg == null) ? await Task.FromResult(42) : await Task.FromResult(43))));
    }
}";

            HasDiagnostic(code, RuleIds.RedundantAwaitRule);
        }

        [Test]
        public void ShouldNotWarnOnTernaryOperatorWhenOnlyOneCaseIsAwaited()
        {
            string code =
@"using System.Threading.Tasks;
public class CustomType
{
    private static Task<int> Foo(int arg)
    {
        return (arg == null ? await Task.FromResult(42) : 1);
    }
}";

            NoDiagnostic(code, RuleIds.RedundantAwaitRule);
        }

        [Test]
        public void ShouldNotWarnOnNonAsyncMethod()
        {
            string code =
@"using System.Threading.Tasks;
public class CustomType
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
        public void ShouldNotWarnOnAwaitWithUnwrap()
        {
            string code =
@"using System.Threading.Tasks;
public class CustomType
{
    private static async Task<int> Foo(int arg)
    {
        return await (await Task.FromResult(42).ContinueWith(t => t));
    }
}";

            NoDiagnostic(code, RuleIds.RedundantAwaitRule);
        }

        [Test]
        public void ShouldNotWarnOnExpressionWithReturn()
        {
            string code =
@"using System.Threading.Tasks;
public class CustomType
{
    private static async Task<int> Foo(string arg)
    {
        return await Task.FromResult(42) + 1;
    }
}";

            NoDiagnostic(code, RuleIds.RedundantAwaitRule);
        }

        [Test]
        public void ShouldNotWarnWhenOneReturnIsNotAwaitable()
        {
            string code =
@"using System.Threading.Tasks;
public class CustomType
{
    private static async Task<int> Foo(int arg)
    {
        if (arg == 42) return 42;

        return await Task.Run(() => 42);
    }
}";

            NoDiagnostic(code, RuleIds.RedundantAwaitRule);
        }

        [Test]
        public void ShouldNotWarnOnConfigureAwaitableTask()
        {
            // ConfigureAwait returns AwaitableTask which is not convertible
            // to task, so warning should be absent, because conversion will break the code
            string code =
@"using System.Threading.Tasks;
public class CustomType
{
    private static async Task<int> Foo(int arg)
    {
        return await Task.FromResult(42).ConfigureAwait(false);
    }
}";

            NoDiagnostic(code, RuleIds.RedundantAwaitRule);
        }

        [Test]
        public void ShouldWarnEvenWhenReturnIsUnreachable()
        {
            string code =
@"using System.Threading.Tasks;
public class CustomType
{
    private static async Task<int> [|Foo|](int arg)
    {
        throw new System.Exception();
        return await Task.FromResult(42);
    }
}";

            HasDiagnostic(code, RuleIds.RedundantAwaitRule);
        }

        [Test]
        public void ShouldWarnWithNestedAsyncLambda()
        {
            string code =
@"using System.Threading.Tasks;
public class CustomType
{
    private static async Task<int> [|Foo|](int arg)
    {
        return await Task.Run(async () =>
        {
            await Task.FromResult(42);
            return 42;
        });
    }
}";

            HasDiagnostic(code, RuleIds.RedundantAwaitRule);
        }

        [Test]
        public void ShouldWarnOnAsyncLambda()
        {
            string code =
@"using System.Threading.Tasks;
public class CustomType
{
    private static Task<int> Foo(int arg)
    {
        return Task.Run([|async|] () =>
        {
            return await Task.FromResult(42);
        });
    }
}";

            HasDiagnostic(code, RuleIds.RedundantAwaitRule);
        }

        [Test]
        public void ShouldWarnOnAsyncDelegate()
        {
            string code =
@"using System.Threading.Tasks;
public class CustomType
{
    private static Task<int> Foo(int arg)
    {
        return Task.Run([|async|] delegate()
        {
            return await Task.FromResult(42);
        });
    }
}";

            HasDiagnostic(code, RuleIds.RedundantAwaitRule);
        }

        [Test]
        public void ShouldNotWarnOnAsyncLambdaWithConfigureAwait()
        {
            string code =
@"using System.Threading.Tasks;
public class CustomType
{
    private static Task<int> Foo(int arg)
    {
        return Task.Run(async () =>
        {
            return await Task.FromResult(42).ConfigureAwait(false);
        });
    }
}";

            NoDiagnostic(code, RuleIds.RedundantAwaitRule);
        }

        [Test]
        public void ShouldWarnOnAsyncLambdaWithExpressionBody()
        {
            string code =
@"using System.Threading.Tasks;
public class CustomType
{
    private static Task<int> Foo(int arg)
    {
        return Task.Run([|async|] () =>
            await Task.FromResult(42);
        );
    }
}";

            HasDiagnostic(code, RuleIds.RedundantAwaitRule);
        }
    }
}
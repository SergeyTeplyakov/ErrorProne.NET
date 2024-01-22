using NUnit.Framework;
using System.Threading.Tasks;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.CancellationTokenRegistrationAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class CancellationTokenRegistrationAnalyzerTests
    {
        [Test]
        public async Task WarnOnTokenAsArgument()
        {
            var test = @"
using System.Threading;
class Test {
    public static void Func(CancellationToken token)
    {
        [|token.Register(() => {})|];
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task WarnOnInstanceCts()
        {
            var test = @"
using System.Threading;
class Test {
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    public void Func()
    {
        var token = _cts.Token;
        [|token.Register(() => { })|];
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task WarnOnStaticCts()
        {
            var test = @"
using System.Threading;
class Test {
    private static readonly CancellationTokenSource _cts = new CancellationTokenSource();
    public void Func()
    {
        var token = _cts.Token;
        [|token.Register(() => { })|];
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task WarnOnInstancePropCts()
        {
            var test = @"
using System.Threading;
class Test {
    private CancellationTokenSource _cts => new CancellationTokenSource();
    private CancellationTokenSource GetCts() => new CancellationTokenSource();
    public void Func()
    {
        var token = _cts.Token;
        [|token.Register(() => { })|];
        [|GetCts().Token.Register(() => { })|];
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task WarnOnInstanceNonCts()
        {
            var test = @"
using System.Threading;
class Test {
    private CancellationToken token;
    private CancellationToken Token => token;
    private CancellationToken GetToken() => token;

    public void Func()
    {
        [|token.Register(() => { })|];
        [|Token.Register(() => { })|];
        [|GetToken().Register(() => { })|];
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarnOnLocalCts()
        {
            var test = @"
using System.Threading;
class Test {
    public static void Func()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        token.Register(() => { });
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarnWhenLocalDeclarationIsSeparateLocalCts()
        {
            var test = @"
using System.Threading;
class Test {
    public static void FooWithCancellation(bool args, CancellationToken databaseClosedCancellationToken)
    {
        CancellationTokenSource? linkedCts = null;
        if (args)
        {
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(databaseClosedCancellationToken);
            linkedCts.Token.Register(() => { });
        }
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarnOnLocalToken()
        {
            var test = @"
using System.Threading;
class Test {
    public static void Func()
    {
        CancellationToken token = default;
        token.Register(() => { });
    }
}
";
            await Verify.VerifyAsync(test);
        }
    }
}
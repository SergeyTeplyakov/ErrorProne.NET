using NUnit.Framework;
using System.Threading.Tasks;
using ErrorProne.NET.DisposableAnalyzers;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.DisposableAnalyzers.DisposeTaskAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using System;

namespace ErrorProne.NET.CoreAnalyzers.Tests.DisposableAnalyzers
{
    [TestFixture]
    public partial class DisoseTaskAnalyzerTests
    {
        private const string Disposable =
            @"
public class Disposable : System.IDisposable
    {
        public void Dispose() { }
        public void Close() {}
        public static Disposable Create() => new Disposable();
    }";

        private static Task VerifyAsync(string code)
        {

            code = $"using System.Threading.Tasks;{Environment.NewLine}{code}{Environment.NewLine}{Disposable}";
            return Verify.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_On_Using_Var_On_Task()
        {
            var test = @"
public class Test
{
    public static async Task ShouldDispose()
    {
        using var x = GetDisposableAsync();
        await Task.Yield();
    }

    private static Task<Disposable> GetDisposableAsync() => null;
}
";
            await VerifyAsync(test);
        }
        
        [Test]
        public async Task Warn_On_Using_On_Task()
        {
            var test = @"
public class Test
{
    public static async Task ShouldDispose()
    {
        await Task.Yield();
        using (GetDisposableAsync())
        {
        }
    }

    private static Task<Disposable> GetDisposableAsync() => null;
}
";
            await VerifyAsync(test);
        }
    }
}

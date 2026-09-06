using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.EventSourceSealedAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class EventSourceSealedAnalyzerTests
    {
        [Test]
        public async Task Warn_On_Non_Sealed_Class()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public class [|DemoEventSource|] : System.Diagnostics.Tracing.EventSource
{
}";

            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task NoWarn_On_Sealed_Or_Abstract()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public abstract class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
}

[System.Diagnostics.Tracing.EventSource(Name = ""Demo2"")]
public sealed class DemoEventSource2 : System.Diagnostics.Tracing.EventSource
{
}";

            await VerifyCS.VerifyAsync(code);
        }
    }
}
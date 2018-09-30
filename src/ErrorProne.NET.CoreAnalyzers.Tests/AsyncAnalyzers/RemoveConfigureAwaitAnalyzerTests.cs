using ErrorProne.NET.AsyncAnalyzers;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class RemoveConfigureAwaitAnalyzerTests : CSharpAnalyzerTestFixture<RemoveConfigureAwaitAnalyzer>
    {
        public const string DiagnosticId = RemoveConfigureAwaitAnalyzer.DiagnosticId;

        [Test]
        public void Warn_For_Task_Delay()
        {
            string code = @"
[assembly:DoNotUseConfigureAwait()]

public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42).[|ConfigureAwait(false)|];
    }
}
";
            HasDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void Warn_For_Property()
        {
            string code = @"
[assembly:DoNotUseConfigureAwait()]

public class MyClass
{
    private static System.Threading.Tasks.Task MyTask => null;
    public static async System.Threading.Tasks.Task Foo()
    {
       await MyTask.[|ConfigureAwait(false)|];
    }
}
";
            HasDiagnostic(code, DiagnosticId);
        }
    }
}
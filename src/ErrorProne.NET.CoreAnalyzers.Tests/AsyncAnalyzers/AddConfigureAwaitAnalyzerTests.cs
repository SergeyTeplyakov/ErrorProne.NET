using ErrorProne.NET.Core.AsyncAnalyzers;
using ErrorProne.NET.Core.CoreAnalyzers;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Core.Tests.AsyncAalyzersTests
{
    [TestFixture]
    public class AddConfigureAwaitAnalyzerTests : CSharpAnalyzerTestFixture<AddConfigureAwaitAnalyzer>
    {
        public const string DiagnosticId = AddConfigureAwaitAnalyzer.DiagnosticId;

        [Test]
        public void Warn_For_Task_Delay()
        {
            string code = @"
[assembly:UseConfigureAwaitFalse()]

public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       [|await System.Threading.Tasks.Task.Delay(42)|];
    }
}
";
            HasDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void NoWarn_For_Task_Yield()
        {
            string code = @"
[assembly:UseConfigureAwaitFalse()]

public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Yield();
    }
}
";
            NoDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void Warn_For_Property()
        {
            string code = @"
[assembly:UseConfigureAwaitFalse()]

public class MyClass
{
    private static System.Threading.Tasks.Task MyTask => null;
    public static async System.Threading.Tasks.Task Foo()
    {
       [|await MyTask|];
    }
}
";
            HasDiagnostic(code, DiagnosticId);
        }
    }
}
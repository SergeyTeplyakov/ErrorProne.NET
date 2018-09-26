using ErrorProne.NET.AsyncAnalyzers;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class NullConditionalOperatorAnalyzerTests : CSharpAnalyzerTestFixture<NullConditionalOperatorAnalyzer>
    {
        public const string DiagnosticId = NullConditionalOperatorAnalyzer.DiagnosticId;

        [Test]
        public void Warn_For_Null_Conditional()
        {
            string code = @"
public class MyClass
{
    public async System.Threading.Tasks.Task Foo(MyClass m)
    {
       [|await m?.Foo(null)|];
    }
}
";
            HasDiagnostic(code, DiagnosticId);
        }
   }
}
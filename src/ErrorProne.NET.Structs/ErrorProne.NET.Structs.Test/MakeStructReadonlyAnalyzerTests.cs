using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Structs.Test
{
    [TestFixture]
    public class MakeStructReadonlyAnalyzerTests : CSharpAnalyzerTestFixture<MakeStructReadonlyAnalyzer>
    {
        public const string DiagnosticId = MakeStructReadonlyAnalyzer.DiagnosticId;

        [Test]
        public void HasDiagnosticsForEmptyStruct()
        {
            string code = @"struct [|FooBar|] {}";
            HasDiagnostic(code, DiagnosticId);
        }
    }
}
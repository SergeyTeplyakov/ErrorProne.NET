using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class ResourceLifetimeAnalyzerTests : CSharpAnalyzerTestFixture<UnobservedResultAnalyzer>
    {
        public const string DiagnosticId = ResourceLifetimeAnalyzer.DiagnosticId;

        [Test]
        public void Warn_When_Disposable_Instance_Is_Used_By_Iterator()
        {
            string code = @"
class Program
{
    static System.Collections.Generic.IEnumerable<int> Foo(string path)
    {
        using (var fs = System.IO.File.OpenRead(path))
        {
            return [|Read(fs)|];
        }
    }

    static System.Collections.Generic.IEnumerable<int> Read(System.IO.Stream s)
    {
        yield return 1;
    }
}";
            HasDiagnostic(code, DiagnosticId);
        }
    }
}
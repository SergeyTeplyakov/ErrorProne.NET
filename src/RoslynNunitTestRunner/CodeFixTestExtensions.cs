using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace RoslynNUnitTestRunner
{
    public static class CodeFixTestExtensions
    {
        public static TTest WithoutGeneratedCodeVerification<TTest>(this TTest test)
            where TTest : CodeFixTest<NUnitVerifier>
        {
            test.Exclusions &= ~AnalysisExclusions.GeneratedCode;
            return test;
        }
    }
}

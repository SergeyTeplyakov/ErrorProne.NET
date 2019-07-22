using ErrorProne.NET.CoreAnalyzers.Allocations;
using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.Allocations.ImiplicitEnumeratorBoxingAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class ImplicitEnumeratorBoxingAnalyzerTests
    {
        [Test]
        public async Task Foreach_On_Interface_Causes_Boxing()
        {
            string code = @"
using System.Collections.Generic;

class A {
	static void M(IList<string> list)
    {
        foreach(var e in [|list|])
        {
            System.Console.WriteLine(e);
        }
    }
}";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

    }
}
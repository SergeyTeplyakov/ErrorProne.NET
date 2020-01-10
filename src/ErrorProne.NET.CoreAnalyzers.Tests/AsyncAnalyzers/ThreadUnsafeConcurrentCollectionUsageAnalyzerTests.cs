using System.Collections.Generic;
using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.ConcurrentCollectionAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class ThreadUnsafeConcurrentCollectionUsageAnalyzerTests
    {
        [Test]
        public async Task Warn_On_ConcurrentDictionary_OrderBy()
        {
            var list = new List<int>();
            string code = @"
using System.Linq;
public class MyClass
{
    public static void Foo()
    {
       var cd = new System.Collections.Concurrent.ConcurrentDictionary<int, string>();
       var sequence = [|System.Linq.Enumerable.OrderBy(cd, kvp => kvp.Key)|];
       sequence = [|cd.OrderBy(kvp => kvp.Key)|];
    }
}";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
    }
}
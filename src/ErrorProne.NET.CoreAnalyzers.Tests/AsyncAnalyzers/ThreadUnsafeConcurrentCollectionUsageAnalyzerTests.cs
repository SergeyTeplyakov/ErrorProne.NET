using System.Collections.Generic;
using System.Linq;
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
       sequence = [|cd.OrderByDescending(kvp => kvp.Key)|];
    }
}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
        
        [Test]
        public async Task NoWarn_On_ConcurrentDictionary_Keys_OrderBy()
        {
            var list = new List<int>();
            string code = @"
using System.Linq;
public class MyClass
{
    public static void Foo()
    {
       var cd = new System.Collections.Concurrent.ConcurrentDictionary<int, string>();
       var r1 = cd.Keys.OrderBy(kvp => kvp);
       var r2 = cd.Values.ToArray();
    }
}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
        
        [Test]
        public async Task Warn_On_ConcurrentDictionary_ToList_And_ToArray()
        {
            string code = @"
using System.Linq;
public class MyClass
{
    public static void Foo()
    {
       var cd = new System.Collections.Concurrent.ConcurrentDictionary<int, string>();
       var r1 = [|System.Linq.Enumerable.ToList(cd)|];
       var r2 = [|System.Linq.Enumerable.ToArray(cd)|];
       var r3 = [|cd.ToList()|];
       var r4 = cd.ToArray(); // This is an instance method, so it is fine!
    }
}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
    }
}
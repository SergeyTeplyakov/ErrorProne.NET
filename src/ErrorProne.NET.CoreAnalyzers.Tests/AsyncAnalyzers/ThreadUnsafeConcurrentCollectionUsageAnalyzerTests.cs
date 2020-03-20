using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.ConcurrentCollectionAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using ErrorProne.NET.TestHelpers;

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
    private static System.Collections.Concurrent.ConcurrentDictionary<int, string> cd = new System.Collections.Concurrent.ConcurrentDictionary<int, string>();
    public static void Foo()
    {
       var sequence = [|System.Linq.Enumerable.OrderBy(cd, kvp => kvp.Key)|];
       sequence = [|cd.OrderBy(kvp => kvp.Key)|];
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
    private static System.Collections.Concurrent.ConcurrentDictionary<int, string> cd = new System.Collections.Concurrent.ConcurrentDictionary<int, string>();
    public static void Foo()
    {
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
        public async Task NoWarn_On_ConcurrentDictionary_Local()
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
    private static System.Collections.Concurrent.ConcurrentDictionary<int, string> cd = new System.Collections.Concurrent.ConcurrentDictionary<int, string>();

    public static void Foo()
    {
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
        
        [Test]
        public async Task Warn_On_ConcurrentDictionary_DifferentSources_ToList()
        {
            string code = @"
using System.Linq;
using CD = System.Collections.Concurrent.ConcurrentDictionary<int, string>;
public class MyClass
{
    private static CD field = null;
    private static CD Prop => null;
    private static CD Method() => null;

    public static void Foo(CD arg)
    {
       var r1 = [|arg.ToList()|];
       r1 = [|field.ToList()|];
       r1 = [|Prop.ToList()|];
       r1 = [|Method().ToList()|];
    }
}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
    }
}
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.SetContainsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using ErrorProne.NET.TestHelpers;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class SetContainsAnalyzerTests
    {
        [Test]
        public async Task No_Warn_On_Enumerable_Contains()
        {
            string code = @"
using System.Collections.Generic;
using System.Linq;
public class MyClass
{
    private static HashSet<string> cd = new HashSet<string>();
    public static void Foo(string str)
    {
        bool fine = cd.Contains(str);
        bool notFine = System.Linq.Enumerable.Contains(cd, str);
    }

}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
        
        [Test]
        public async Task Warn_On_Enumerable_Contains_With_Parameter()
        {
            string code = @"
using System.Collections.Generic;
using System.Linq;
public class MyClass
{
    public static void Foo(HashSet<string> cd, string str)
    {
        bool fine = cd.Contains(str);
        bool notFine = [|cd.Contains(str, System.StringComparer.Ordinal)|];
    }

}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
        
        [Test]
        public async Task Warn_On_Enumerable_Contains_With_Local()
        {
            string code = @"
using System.Collections.Generic;
using System.Linq;
public class MyClass
{
    public static void Foo(string str)
    {
        HashSet<string> cd = new HashSet<string>();
        bool fine = cd.Contains(str);
        bool notFine = [|cd.Contains(str, System.StringComparer.Ordinal)|];
    }

}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
        
        [Test]
        public async Task Warn_On_HashSet_Of_String_With_Extension_Method_Contains()
        {
            string code = @"
using System.Collections.Generic;
using System.Linq;
public class MyClass
{
    private static HashSet<string> cd = new HashSet<string>();
    public static void Foo(string str)
    {
        bool fine = cd.Contains(str);
        bool notFine = [|System.Linq.Enumerable.Contains(cd, str, System.StringComparer.Ordinal)|];
    }

}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
        
        [Test]
        public async Task Warn_On_HashSet_Of_String_Contains_And_StringComparison()
        {
            string code = @"
using System;
using System.Collections.Generic;
using System.Linq;
public class MyClass
{
    private static HashSet<string> cd = new HashSet<string>();
    public static void Foo(string str)
    {
        bool fine = cd.Contains(str);
        bool notFine = [|cd.Contains(str, StringComparer.CurrentCulture)|];
    }
}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
        
        [Test]
        public async Task Warn_On_ISet_Of_String_With_Extension_Method_Contains()
        {
            string code = @"
using System.Collections.Generic;
using System.Linq;
public class MyClass
{
    private static ISet<string> cd = new HashSet<string>();
    public static void Foo(string str)
    {
        bool fine = cd.Contains(str);
        bool notFine = [|System.Linq.Enumerable.Contains(cd, str, System.StringComparer.Ordinal)|];
    }

}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
        
        [Test]
        public async Task Warn_On_CustomSet()
        {
            string code = @"
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections;

public class MySet<T> : ISet<T>
    {
        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <inheritdoc />
        void ICollection<T>.Add(T item)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void ExceptWith(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void IntersectWith(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool IsSubsetOf(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool IsSupersetOf(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool Overlaps(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool SetEquals(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void UnionWith(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        bool ISet<T>.Add(T item)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Clear()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool Contains(T item)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool Remove(T item)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public int Count { get; }

        /// <inheritdoc />
        public bool IsReadOnly { get; }
    }

public class MyClass
{
    private static MySet<string> cd = new MySet<string>();
    public static void Foo(string str)
    {
        bool fine = cd.Contains(str);
        bool notFine = [|System.Linq.Enumerable.Contains(cd, str, StringComparer.Ordinal)|];
    }

}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } }
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
    }
}
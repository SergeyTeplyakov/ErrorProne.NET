using NUnit.Framework;
using System.Threading.Tasks;
using ErrorProne.NET.DisposableAnalyzers;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.DisposableAnalyzers.DisposeBeforeLoosingScopeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.DisposableAnalyzers
{
    [TestFixture]
    public partial class DisposeBeforeLoosingScopeAnalyzerTests
    {
        [Test]
        public async Task Warn_On_No_Dispose()
        {
            var test = @"
public class Test
{
    public static void ShouldDispose()
    {
        var [|d1|] = new Disposable();
        var nd = new NonDisposable();
    }

    public class Disposable : System.IDisposable
    {
        public void Dispose() { }
    }

    public class NonDisposable { }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task Warn_On_No_Dispose_With_Cast()
        {
            var test = @"
public class Test
{
    public static void ShouldDispose()
    {
        var [|d1|] = new Disposable() as object;
        var [|d2|] = (object)new Disposable();
    }

    public class Disposable : System.IDisposable
    {
        public void Dispose() { }
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task Warn_On_No_Dispose_In_If_Block()
        {
            var test = @"
public class Test
{
    public static void ShouldDispose()
    {
        if ([|new Disposable()|] is null)
        {
        }
    }

    public class Disposable : System.IDisposable
    {
        public void Dispose() { }
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task Warn_On_No_Dispose_With_Factory_Method()
        {
            var test = @"
public class Test
{
    public static void ShouldDispose()
    {
        var [|d|] = Disposable.Create();
    }

    public class Disposable : System.IDisposable
    {
        public void Dispose() { }
        public static Disposable Create() => new Disposable();
    }

    public class NonDisposable { }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_No_Dispose_With_Property()
        {
            var test = @"
public class Test
{
    public static void ShouldDispose()
    {
        var d = Instance;
    }

    public static Disposable Instance => new Disposable().ReleaseOwnership();
}
";
            await VerifyAsync(test);
        }
        
        [Test]
        public async Task Warn_On_No_Dispose_With_Factory_Property()
        {
            var test = @"
public class Test
{
    public static void ShouldDispose()
    {
        var [|d|] = Disposable.Instance;
    }

    public class Disposable : System.IDisposable
    {
        public void Dispose() { }

        [ReturnsOwnership]
        public static Disposable Instance => new Disposable();
    }

    public class NonDisposable { }
}
";
            await VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_UsingDeclaration()
        {
            var test = @"
public class Test
{
    public static void ShouldDispose()
    {
        using var d = new Disposable();
        using var _ = new Disposable();
    }

    public class Disposable : System.IDisposable
    {
        public void Dispose() { }
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Manual_Dispose()
        {
            var test = @"
public class Test
{
    public static void ShouldDispose()
    {
        var d = new Disposable();
        d.Dispose();
    }

    public class Disposable : System.IDisposable
    {
        public void Dispose() { }
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Usings()
        {
            var test = @"
public class Test
{
    public static void ShouldDispose()
    {
        using var d1 = new Disposable();
        using var d2 = Disposable.Create();
        using var d3 = Disposable.Instance;
        using var _ = new Disposable();
    }

    public class Disposable : System.IDisposable
    {
        public void Dispose() { }
        public static Disposable Create() => new Disposable();
        public static Disposable Instance => new Disposable();
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_On_SimpleReturn()
        {
            var test = @"
public class Test
{
    public class Disposable : System.IDisposable
    {
        public void Dispose() { }
        public static Disposable Create() => new Disposable();
        public static Disposable Create2()
        {
            return new Disposable();
        }
        public static Disposable Instance => new Disposable();
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Conditional_Return()
        {
            var test = @"
public class Test
{
    public class Disposable : System.IDisposable
    {
        public void Dispose() { }
        public static Disposable Create2(bool check)
        {
            var result = new Disposable();
            if (check)
            {
                return result;
            }
            else
            {
                return result;
            }
        }
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_On_Conditional_ReturnInCatchFinally()
        {
            var test = @"
public class Test
{
    public class Disposable : System.IDisposable
    {
        public void Dispose() { }
        public static Disposable Create2(bool check)
        {
            var result = new Disposable();
            try
            {
                return result;
            }
            catch
            {
                return result;
            }
        }
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Conditional_Return_Conditionally_In_All_Branches()
        {
            var test = @"
public class Test
{
    public class Disposable : System.IDisposable
    {
        public void Dispose() { }
        public static Disposable Create2(bool check)
        {
            var result = new Disposable();
            try
            {
                if (check)
                {
                    return result;
                }
                else
                {
                    return result;
                }   
            }
            catch
            {
                return result;
            }
        }
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_On_UsingStatement()
        {
            var test = @"
public class Test
{
    public static void ShouldDispose()
    {
        using (var d = new Disposable())
        using (var d2 = new Disposable())
        using (var d3 = Disposable.Create())
        {
        }

    }

    public class Disposable : System.IDisposable
    {
        public void Dispose() { }
        public static Disposable Create() => new Disposable();
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_UsingStatement_In_Local()
        {
            var test = @"
public class Test
{
    public static void ShouldDispose()
    {
        void useInHelper()
        {
            using (var d4 = Disposable.Create())
            {
            }
        }

        System.Threading.Tasks.Task.Run(() =>
        {
            using (var d5 = Disposable.Create())
            {
            }
        });
    }
}
";
            await VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_NullableDispose()
        {
            var test = @"
public class Test
{
    public static void ShouldDispose(bool shouldCreate)
    {
        Disposable d = null;
        try
        {
            if (shouldCreate)
            {
                d = new Disposable();
            }
        }
        finally
        {
            d?.Dispose();   
        }
    }
}
";
            await VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_On_Nullable_Factory()
        {
            var test = @"
public class Test
{
    public static Disposable TryCreate(bool shouldCreate)
    {
        
        try
        {
            Disposable d = new Disposable();
            return d;
        }
        catch
        {
            return null;
        }
    }
}
";
            await VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_On_StreamReader()
        {
            var test = @"
public class Test
{
    public static void ShouldDispose()
    {
        using (var fs = new System.IO.FileStream(string.Empty, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
        using (var sr = new System.IO.StreamReader(fs))
        {
        }
    }

    public class Disposable : System.IDisposable
    {
        public void Dispose() { }
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Activity()
        {
            var test = @"
public class Test
{
    public class Activity : System.IDisposable
    {
        public Activity SetTag(string key, object value) => this;
        public void Dispose() { }
    }

    public static void ActivityCase(Activity a)
    {
        a.SetTag(""42"", 42);
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Dispose_In_Finally()
        {
            var test = @"
public class Test
{
    public static void ShouldDispose()
    {
        var d = new Disposable();
        try
        {
        }
        finally
        {
            d.Dispose();
        }
    }

    public class Disposable : System.IDisposable
    {
        public void Dispose() { }
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Close_In_Finally()
        {
            var test = @"
public class Test
{
    public static void ShouldDispose(bool create)
    {
        Disposable d = null;

        try        
        {
            if (create)
            {
                d = new Disposable();
            }
        }
        finally
        {
            if (d != null)
                d.Close();
        }
        
    }
}
";
            await VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Nested_Using()
        {
            var test = @"
public class Test
{
    public static void ShouldDispose(bool create)
    {
        Disposable d = null;

        if (create)
        {
            d = new Disposable();
        }

        using(d)
        {
        }
    }
}
";
            await VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Assigning_To_Field()
        {
            var test = @"
public class Test
{
    private Disposable _d;
    public void ShouldDispose(bool create)
    {
        Disposable d = null;

        if (create)
        {
            d = new Disposable();
        }

        _d = d;
    }
}
";
            await VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Assigning_To_Field_With_Interlocked()
        {
            var test = @"
public class Test
{
    private Disposable _d;
    public void ShouldDispose(bool create)
    {
        Disposable d = null;

        if (create)
        {
            d = new Disposable();
        }

        System.Threading.Interlocked.Exchange(ref _d, d);
    }
}
";
            await VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Assigning_To_Field_In_Constructor()
        {
            var test = @"
public class Test
{
    private Disposable _d;
    private Disposable _d2;
    public Test(bool create)
    {
        Disposable d = null;

        if (create)
        {
            d = new Disposable();
        }

        _d = d;
        _d2 = new Disposable();
    }
}
";
            await VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Dispose_In_Try_And_Catch()
        {
            var test = @"
public class Test
{
    public static void ShouldDispose()
    {
        var d = new Disposable();
        try
        {
            d.Dispose();
        }
        catch
        {
            d.Dispose();
        }
    }

    public class Disposable : System.IDisposable
    {
        public void Dispose() { }
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Disposable_Returned_In_Func()
        {
            var test = @"
public class Test
{
    internal static System.Func<Disposable> CreateInstance = () => new Disposable();
}
";
            await VerifyAsync(test);
        }
        
        // await using (var buildCoordinator = new BuildCoordinator(
        
        [Test]
        public async Task NoWarn_On_Disposable_Returned_In_Func_InTask()
        {
            var test = @"
public class Test
{
    public static void TestTask()
    {
        System.Threading.Tasks.Task.Run(() =>
        {
            var d = new Disposable();
            return d;
        });
    }
}";
            await VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Type_Erasure()
        {
            // This should be covered by another rule.
            var test = @"
public interface IFoo { }

public class Foo : IFoo, System.IDisposable
{
    public void Dispose() { }
    public static IFoo Create() => new Foo();
}
";
            await VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Yield_Return()
        {
            var test = @"
public class Test
{
    public static System.Collections.Generic.IEnumerable<Disposable> Get()
    {
        yield return new Disposable();
    }
}
";
            await VerifyAsync(test);
        }
        
        // [Test] Not supported yet.
        public async Task Warn_On_DisposeInFinally_Conditionally()
        {
            var test = @"
public class Test
{
    public static void ShouldDispose(bool shouldDispose)
    {
        var [|d|] = new Disposable();
        try
        {
        }
        finally
        {
            if (shouldDispose)
                d.Dispose();
        }
    }

    public class Disposable : System.IDisposable
    {
        public void Dispose() { }
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Dispose_In_Try_Catch()
        {
            var test = @"
public class Test
{
    public static void ShouldDispose()
    {
        var d = new Disposable();
        try
        {
        }
        finally
        {
            d.Dispose();
        }
    }

    public class Disposable : System.IDisposable
    {
        public void Dispose() { }
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_UsingStatement_With_No_Local()
        {
            var test = @"
public class Test
{
    public static void ShouldDispose()
    {
        using (new Disposable())
        {
        }
    }

    public class Disposable : System.IDisposable
    {
        public void Dispose() { }
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Field()
        {
            var test = @"
public class Test
{
    private readonly Disposable _d = new Disposable();

    public class Disposable : System.IDisposable
    {
        public void Dispose() { }
    }
}
";
            await Verify.VerifyAsync(test);
        }


        // Positive test cases:
        // * var d = new Disposable()
        // * var d = CreateDisposable();
        // * var d = await CreateDisposableAsync();
        // * var d = DisposableProperty;
        // * var d = FooBar.DisposableProperty;

        // Disposed only in 'catch' block

        // Dispose if a type has 'Dispose' method and is ref struct?

        // Negative test cases

        // Configure a list of disposable types that should not be disposed.
    }
}

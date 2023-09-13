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

        [ReleasesOwnership]
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

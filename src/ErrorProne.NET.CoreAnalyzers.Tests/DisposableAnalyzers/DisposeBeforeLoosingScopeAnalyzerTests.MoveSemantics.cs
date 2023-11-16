using System;
using NUnit.Framework;
using System.Threading.Tasks;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.DisposableAnalyzers.DisposeBeforeLoosingScopeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.DisposableAnalyzers
{
    [TestFixture]
    public partial class DisposeBeforeLoosingScopeAnalyzerTests
    {
        private const string AcquiresOwnershipAttribute =
            @"
[System.AttributeUsage(System.AttributeTargets.Parameter)]
public class AcquiresOwnershipAttribute : System.Attribute { }

[System.AttributeUsage(System.AttributeTargets.All)]
public class ReturnsOwnershipAttribute : System.Attribute { }

[System.AttributeUsage(System.AttributeTargets.Method)]
public class KeepsOwnershipAttribute : System.Attribute { }

public static class DisposableExtensions
{
    /// <summary>
    /// A special method that allows to release the current ownership.
    /// </summary>
    [KeepsOwnership]
    public static T ReleaseOwnership<T>([AcquiresOwnership]this T disposable) where T : System.IDisposable
    {
        return disposable;
    }

    public static T ThrowIfNull<T>(this T t) => t;
}";

        private const string Disposable =
            @"
public class Disposable : System.IDisposable
    {
        public void Dispose() { }
        public void Close() {}
        public static Disposable Create() => new Disposable();
    }";

        [Test]
        public async Task NoWarn_On_Move()
        {
            var test = @"
public class Test
{
    public static void Moves()
    {
        var d = new Disposable();
        TakesOwnership(d);
    }

    private static void TakesOwnership([AcquiresOwnership] Disposable d2) { d2.Dispose(); }
}
";
            await VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Out_Variable()
        {
            var test = @"
public class Test
{
    public void Moves(out Disposable d)
    {
        d = new Disposable();
    }
}
";
            await VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_ActivitySource_StartActivity()
        {
            var test = @"
public class Test
{
    public static void StartActivityCase()
    {
        new System.Diagnostics.ActivitySource(""name"").StartActivity();
    }
}
";
            await VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_On_Move_To_List()
        {
            var test = @"
public class Test
{
    public static void Moves()
    {
        var d = new Disposable();
        var list = new System.Collections.Generic.List<Disposable>(){ d.ReleaseOwnership() };   
    }
}
";
            await VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Move_With_ReleaseOwnership()
        {
            var test = @"
public class Test
{
    public static void Moves()
    {
        var d = new Disposable().ReleaseOwnership();
    }
}
";
            await VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_ThrowIf()
        {
            var test = @"
public class Test
{
    public static void Moves(Disposable d)
    {
        d.ThrowIfNull();
    }
}
";
            await VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_On_Non_FactoryMethod()
        {
            var test = @"
public class Test
{
    public static void Moves()
    {
        var d = NoOwnership();
    }

    [KeepsOwnershipAttribute]
    private static Disposable NoOwnership() => new Disposable().ReleaseOwnership();
}
";
            await VerifyAsync(test);
        }

        [Test]
        public async Task Warn_On_Taken_Ownership()
        {
            var test = @"
public class Test
{
    private static void TakesOwnership([AcquiresOwnership] Disposable [|d|]) { }
}
";
            await VerifyAsync(test);
        }
        
        [Test]
        public async Task Warn_On_Taken_Ownership_With_Usage()
        {
            var test = @"
public class Test
{
    private static string TakesOwnership([AcquiresOwnership] Disposable [|d|])
    {
        return d.ToString();
    }
}
";
            await VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Taken_Ownership_With_Dispose()
        {
            var test = @"
public class Test
{
    private static string TakesOwnership([AcquiresOwnership] Disposable d)
    {
        var r = d.ToString();
        d.Dispose();
        return r;
    }
}
";
            await VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Taken_Ownership_With_Using()
        {
            var test = @"
public class Test
{
    private static string TakesOwnership([AcquiresOwnership] Disposable d)
    {
        using (d)
        {
            return d.ToString();    
        }
    }
}
";
            await VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarn_On_Taken_Ownership_With_Moved_Ownership()
        {
            var test = @"
public class Test
{
    private static string TakesOwnershipAndDisposes([AcquiresOwnership] Disposable d)
    {
        using (d)
        {
            return d.ToString();    
        }
    }

    private static string TakesOwnership([AcquiresOwnership] Disposable d)
    {
        return TakesOwnershipAndDisposes(d);
    }
}
";
            await VerifyAsync(test);
        }
        
        [Test]
        public async Task Warn_On_Taken_Ownership_With_Incorrect_Moved_Ownership()
        {
            var test = @"
public class Test
{
    private static string TakesOwnershipAndDisposes(Disposable d)
    {
        using (d)
        {
            return d.ToString();    
        }
    }

    private static string TakesOwnership([AcquiresOwnership] Disposable [|d|])
    {
        return TakesOwnershipAndDisposes(d);
    }
}
";
            await VerifyAsync(test);
        }

        private static Task VerifyAsync(string code)
        {
            code += $"{Environment.NewLine}{AcquiresOwnershipAttribute}{Environment.NewLine}{Disposable}";
            return Verify.VerifyAsync(code);
        }
    }
}
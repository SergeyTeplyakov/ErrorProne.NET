using System.Threading.Tasks;
using NUnit.Framework;

namespace ErrorProne.NET.CoreAnalyzers.Tests.DisposableAnalyzers;

public partial class DisposeBeforeLosingScopeAnalyzerTests
{
    [TestCase("=> new Disposable();")]
    [TestCase("{ return new Disposable(); }")]
    [TestCase("=> (System.IDisposable)(new Disposable());")]
    public Task Fresh_Return_Creates_A_Caller_Obligation(string implementation)
    {
        return VerifyAsync(@"
public class Test
{
    private static System.IDisposable Create() " + implementation + @"
    public void Run() { var {|ERP044:resource|} = Create(); }
}");
    }

    [TestCase("=> shared;")]
    [TestCase("=> null;")]
    [TestCase("=> condition ? new Disposable() : shared;")]
    [TestCase("{ if (condition) return new Disposable(); return shared; }")]
    [TestCase("{ var resource = new Disposable(); return resource; }")]
    [TestCase("{ Prepare(); return new Disposable(); }")]
    [TestCase("=> Disposable.Create();")]
    [TestCase("=> Create();")]
    public Task Oblivious_Result_Stays_Unknown_Without_A_Direct_Fresh_Return(string implementation)
    {
        return VerifyAsync(@"
public class Test
{
    private static Disposable shared;
    private static bool condition;
    private static void Prepare() { }
    private static Disposable Create() " + implementation + @"
    public void Run() { var resource = Create(); }
}");
    }

    [TestCase("var resource = System.IO.File.OpenRead(\"path\");")]
    [TestCase("using var resource = System.IO.File.OpenRead(\"path\");")]
    [TestCase("var resource = GetShared(); resource.Dispose();")]
    public Task Oblivious_Result_Does_Not_Imply_Ownership_Or_Borrowing(string body)
    {
        return VerifyAsync(@"
public class Test
{
    private static Disposable shared;
    private static Disposable GetShared() => shared;
    public void Run() { " + body + @" }
}");
    }

    [TestCase("public virtual", true, false)]
    [TestCase("public override", false, false)]
    [TestCase("public sealed override", false, true)]
    public Task Fresh_Return_Requires_A_Non_Overridable_Target(string modifiers, bool baseMethod, bool owned)
    {
        return VerifyAsync(@"
public class Base
{
    public virtual Disposable Create() => new Disposable();
}
public class Test " + (baseMethod ? "" : ": Base") + @"
{
    " + modifiers + @" Disposable Create() => new Disposable();
    public void Run()
    {
        var " + (owned ? "{|ERP044:resource|}" : "resource") + @" = Create();
    }
}");
    }

    [Test]
    public Task Fresh_Return_Is_Inferred_For_An_Override_In_A_Sealed_Type()
    {
        return VerifyAsync(@"
public abstract class Base { public abstract Disposable Create(); }
public sealed class Test : Base
{
    public override Disposable Create() => new Disposable();
    public void Run() { var {|ERP044:resource|} = Create(); }
}");
    }

    [TestCase("var {|ERP044:copy|} = original.Clone(); original.Dispose();")]
    [TestCase("var copy = original.Clone(); copy.Dispose(); original.Dispose();")]
    public Task Fresh_Return_Is_Not_A_Fluent_Receiver_Alias(string body)
    {
        return VerifyAsync(@"
public class Resource : System.IDisposable
{
    public void Dispose() { }
    public Resource Clone() => new Resource();
}
public class Test
{
    public void Run()
    {
        var original = new Resource();
        " + body + @"
    }
}");
    }

    [Test]
    public Task Fresh_Return_Disposal_Does_Not_Discharge_The_Receiver()
    {
        return VerifyAsync(@"
public class Resource : System.IDisposable
{
    public void Dispose() { }
    public Resource Clone() => new Resource();
}
public class Test
{
    public void Run()
    {
        var {|ERP044:original|} = new Resource();
        var copy = original.Clone();
        copy.Dispose();
    }
}");
    }

    [TestCase("[return: DoNotDispose]")]
    [TestCase("[KeepsOwnership]")]
    public Task Fresh_Return_Does_Not_Override_An_Explicit_Borrowing_Contract(string attribute)
    {
        return VerifyAsync(@"
public class Test
{
    " + attribute + @"
    private static Disposable Create() => new Disposable();
    public void Run() { var resource = Create(); {|ERP046:resource|}.Dispose(); }
}");
    }

    [Test]
    public Task Oblivious_Result_Can_Be_Made_Explicitly_Owning()
    {
        return VerifyAsync(@"
public class Test
{
    [return: ReturnsOwnership]
    private static Disposable Create() => null;
    public void Run() { var {|ERP044:resource|} = Create(); }
}");
    }

    [Test]
    public Task Fresh_Return_Supports_Disposable_Structs_And_Constrained_Generics()
    {
        return VerifyAsync(@"
public struct Resource : System.IDisposable { public void Dispose() { } }
public class Test
{
    private static System.IDisposable CreateStruct() => new Resource();
    private static T Create<T>() where T : System.IDisposable, new() => new T();
    public void Run()
    {
        var {|ERP044:structure|} = CreateStruct();
        var {|ERP044:generic|} = Create<Disposable>();
    }
}");
    }

    [TestCase("var {|ERP044:resource|} = await Create();")]
    [TestCase("await using var resource = await Create().ConfigureAwait(false);")]
    public Task Fresh_Return_Supports_Direct_Async_Factories(string body)
    {
        return VerifyAsync(@"
public class Resource : System.IAsyncDisposable
{
    public System.Threading.Tasks.ValueTask DisposeAsync() => default;
}
public class Test
{
    private static async System.Threading.Tasks.Task<Resource> Create() => new Resource();
    public async System.Threading.Tasks.Task Run() { " + body + @" }
}");
    }

    [TestCase("new Source()")]
    [TestCase("(dynamic){|ERP044:new Source()|}")]
    public Task Oblivious_Result_Does_Not_Treat_A_User_Conversion_As_A_Fresh_Return(string expression)
    {
        return VerifyAsync(@"
public class Source : System.IDisposable
{
    private static Disposable shared;
    public void Dispose() { }
    public static implicit operator Disposable(Source value) { value.Dispose(); return shared; }
}
public class Test
{
    private static Disposable Create() => " + expression + @";
    public void Run() { var resource = Create(); }
}");
    }

    [Test]
    public Task Fresh_Return_Respects_The_Disposable_Type_Exemptions()
    {
        return VerifyAsync(@"
public class Test
{
    private static System.IDisposable Create() => new System.IO.MemoryStream();
    public void Run() { var resource = Create(); }
}");
    }

    [Test]
    public Task Fresh_Return_Supports_Generic_Extensions_Without_Receiver_Aliasing()
    {
        return VerifyAsync(@"
public static class Factories
{
    public static T FreshCopy<T>(this T value) where T : System.IDisposable, new() => new T();
}
public class Test
{
    public void Run()
    {
        var {|ERP044:original|} = new Disposable();
        var copy = original.FreshCopy();
        copy.Dispose();
    }
}");
    }

    [Test]
    public Task Fresh_Return_Uses_The_Partial_Implementation()
    {
        return VerifyAsync(@"
public partial class Api { public static partial Disposable Create(); }
public partial class Api { public static partial Disposable Create() => new Disposable(); }
public class Test
{
    public void Run() { var {|ERP044:resource|} = Api.Create(); }
}");
    }

    [Test]
    public Task Oblivious_Result_Of_Virtual_Dispatch_Does_Not_Use_A_Derived_Body()
    {
        return VerifyAsync(@"
public abstract class Base { public abstract Disposable Create(); }
public sealed class Implementation : Base
{
    public override Disposable Create() => new Disposable();
}
public class Test
{
    public void Run()
    {
        Base factory = new Implementation();
        var resource = factory.Create();
    }
}");
    }

    [Test]
    public Task Oblivious_Result_From_Yield_Is_Not_A_Direct_Factory_Return()
    {
        return VerifyAsync(@"
public class Test
{
    private static System.Collections.Generic.IEnumerator<Disposable> Create()
    {
        yield return new Disposable();
    }
    public void Run() { var resource = Create(); }
}");
    }

    [Test]
    public Task Oblivious_Result_Remains_Unknown_After_Await()
    {
        return VerifyAsync(@"
public class Test
{
    private static System.Threading.Tasks.Task<Disposable> Create() => null;
    public async System.Threading.Tasks.Task Run()
    {
        var first = await Create();
        var second = await Create().ConfigureAwait(false);
    }
}");
    }
}

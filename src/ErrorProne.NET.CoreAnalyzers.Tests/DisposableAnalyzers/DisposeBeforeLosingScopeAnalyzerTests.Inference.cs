using System.Threading.Tasks;
using NUnit.Framework;

namespace ErrorProne.NET.CoreAnalyzers.Tests.DisposableAnalyzers;

public partial class DisposeBeforeLosingScopeAnalyzerTests
{
    [TestCase("var {|ERP044:d|} = new Disposable(); var alias = d;")]
    [TestCase("var {|ERP044:d|} = new Disposable(); _ = d;")]
    [TestCase("var {|ERP044:d|} = new Disposable(); d = new Disposable(); d.Dispose();")]
    [TestCase("var d = new Disposable(); var alias = d; alias.Dispose();")]
    [TestCase("var d = new Disposable(); var alias = d; d = null; alias.Dispose();")]
    [TestCase("new Disposable().Dispose();")]
    [TestCase("new Disposable().Close();")]
    [TestCase("var d = new Disposable(); d?.Dispose();")]
    [TestCase("var d = new Disposable(); using (d) { }")]
    [TestCase("var d = new Disposable(); using var alias = d;")]
    [TestCase("Disposable d = null; d?.Dispose(); {|ERP044:d|} = new Disposable();")]
    public Task Tracks_Resources_Through_Local_Aliases(string body)
    {
        return VerifyAsync("public class Test { public void Run() { " + body + " } }");
    }

    [Test]
    public Task Checks_All_Acquired_Parameters()
    {
        return VerifyAsync(@"
public class Test
{
    public void Consume([AcquiresOwnership] Disposable first,
                        [AcquiresOwnership] Disposable {|ERP044:second|})
    {
        first.Dispose();
    }
}");
    }

    [Test]
    public Task Trusts_Declared_Transfer_And_Reports_In_Callee()
    {
        return VerifyAsync(@"
public class Test
{
    public void Run()
    {
        var d = new Disposable();
        Consume(d);
    }
    private void Consume([AcquiresOwnership] Disposable {|ERP044:value|}) { }
}");
    }

    [TestCase("Consume(owned: d, borrowed: null);", false)]
    [TestCase("Consume(owned: null, borrowed: d);", true)]
    public Task Uses_Bound_Parameters_For_Named_Arguments(string call, bool leaks)
    {
        return VerifyAsync(@"
public class Test
{
    public void Run()
    {
        var " + (leaks ? "{|ERP044:d|}" : "d") + @" = new Disposable();
        " + call + @"
    }
    private void Consume(Disposable borrowed, [AcquiresOwnership] Disposable owned)
    {
        owned.Dispose();
    }
}");
    }

    [Test]
    public Task Constructor_Receives_And_Discharges_Ownership()
    {
        return VerifyAsync(@"
public class Test
{
    public void Run() { _ = new Owner(new Disposable()); }
}
public class Owner
{
    public Owner([AcquiresOwnership] Disposable value) { value.Dispose(); }
}");
    }

    [Test]
    public Task Constructor_Must_Discharge_Annotated_Parameter()
    {
        return VerifyAsync(@"
public class Owner
{
    public Owner([AcquiresOwnership] Disposable {|ERP044:value|}) { }
}");
    }

    [Test]
    public Task Acquiring_Parameters_Do_Not_Hide_Owning_Returns()
    {
        return VerifyAsync(@"
public class Test
{
    public void Run()
    {
        var input = new Disposable();
        var {|ERP044:output|} = Replace(input);
    }
    [ReturnsOwnership]
    private static Disposable Replace([AcquiresOwnership] Disposable value)
    {
        value.Dispose();
        return new Disposable();
    }
}");
    }

    [Test]
    public Task Explicit_Owning_Result_Overrides_Fluent_Inference()
    {
        return VerifyAsync(@"
public class Resource : System.IDisposable
{
    public void Dispose() { }
    [ReturnsOwnership]
    public Resource Clone() => new Resource();
    public void Run()
    {
        var {|ERP044:copy|} = Clone();
    }
}");
    }

    [Test]
    public Task Return_Target_Attribute_Defines_Owning_Result()
    {
        return VerifyAsync(@"
public class Test
{
    public void Run() { var {|ERP044:d|} = Create(); }
    [return: ReturnsOwnership]
    private static Disposable Create() => new Disposable();
}");
    }

    [Test]
    public Task Infers_Forwarding_To_An_Owning_Callee()
    {
        return VerifyAsync(@"
public class Test
{
    public void Run() { Forward(new Disposable()); }
    private static void Forward(Disposable value) { Consume(value); }
    private static void Consume([AcquiresOwnership] Disposable value) { value.Dispose(); }
}");
    }

    [Test]
    public Task Recursive_Inference_Is_Bounded()
    {
        return VerifyAsync(@"
public class Test
{
    public void Run() { Loop({|ERP044:new Disposable()|}); }
    private static void Loop(Disposable value) { Loop(value); }
}");
    }

    [Test]
    public Task Does_Not_Transfer_To_A_Borrowed_Field()
    {
        return VerifyAsync(@"
public class Test
{
    [NoOwnership] private Disposable _borrowed;
    public void Run() { _borrowed = {|ERP044:new Disposable()|}; }
}");
    }

    [TestCase("var {|ERP044:d|} = new Resource();")]
    [TestCase("using var d = new Resource();")]
    [TestCase("var d = new Resource(); d.Dispose();")]
    public Task Recognizes_Disposable_Structs(string body)
    {
        return VerifyAsync(@"
public struct Resource : System.IDisposable { public void Dispose() { } }
public class Test { public void Run() { " + body + " } }");
    }

    [Test]
    public Task Recognizes_Constrained_Generic_Creation()
    {
        return VerifyAsync(@"
public class Test
{
    public void Run<T>() where T : System.IDisposable, new()
    {
        var {|ERP044:value|} = new T();
    }
}");
    }

    [TestCase("var {|ERP044:d|} = new Resource();")]
    [TestCase("var d = new Resource(); await d.DisposeAsync();")]
    [TestCase("await using var d = new Resource();")]
    [TestCase("await using (new Resource()) { }")]
    public Task Recognizes_Async_Disposal(string body)
    {
        return VerifyAsync(@"
public class Resource : System.IAsyncDisposable
{
    public System.Threading.Tasks.ValueTask DisposeAsync() => default;
}
public class Test
{
    public async System.Threading.Tasks.Task Run() { " + body + " } }");
    }

    [TestCase("var {|ERP044:d|} = await Create();")]
    [TestCase("using var d = await Create();")]
    [TestCase("var {|ERP044:d|} = await Create().ConfigureAwait(false);")]
    [TestCase("using var d = await Create().ConfigureAwait(false);")]
    public Task Recognizes_Owned_Awaited_Results(string body)
    {
        return VerifyAsync(@"
public class Test
{
    public async System.Threading.Tasks.Task Run() { " + body + @" }
    [ReturnsOwnership]
    private static System.Threading.Tasks.Task<Disposable> Create() => null;
}");
    }

    [TestCase(false)]
    [TestCase(true)]
    public Task Stream_Wrapper_Respects_LeaveOpen(bool leaveOpen)
    {
        return VerifyAsync(@"
public class Test
{
    public void Run()
    {
        var " + (leaveOpen ? "{|ERP044:stream|}" : "stream") + @" = new System.IO.FileStream(""path"", System.IO.FileMode.Open);
        using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8, true, 1024, "
        + (leaveOpen ? "true" : "false") + @");
    }
}");
    }

    [TestCase("var d = new Disposable(); d.Dispose(); {|ERP046:d|}.ToString();")]
    [TestCase("var d = new Disposable(); var alias = d; d.Dispose(); {|ERP046:alias|}.ToString();")]
    [TestCase("var d = new Disposable(); d.Dispose(); {|ERP046:d|}.Dispose();")]
    [TestCase("var d = new Disposable(); Consume(d); {|ERP046:d|}.ToString();")]
    [TestCase("var d = new Disposable(); d.Dispose(); d = new Disposable(); d.Dispose();")]
    [TestCase("var d = new Disposable(); d.Dispose(); d = null;")]
    public Task Checks_Obvious_Uses_After_Disposal_Or_Transfer(string body)
    {
        return VerifyAsync(@"
public class Test
{
    public void Run() { " + body + @" }
    private static void Consume([AcquiresOwnership] Disposable value) { value.Dispose(); }
}");
    }

    [TestCase("{|ERP046:value|}.Dispose();")]
    [TestCase("{|ERP046:value|}?.Dispose();")]
    public Task Explicitly_Borrowed_Parameter_Cannot_Be_Disposed(string body)
    {
        return VerifyAsync(@"
public class Test
{
    public void Run([NoOwnership] Disposable value)
    {
        " + body + @"
    }
}");
    }

    [Test]
    public Task Explicitly_Borrowed_Field_Cannot_Be_Transferred()
    {
        return VerifyAsync(@"
public class Test
{
    [NoOwnership] private Disposable _borrowed;
    public void Run() { Consume({|ERP046:_borrowed|}); }
    private static void Consume([AcquiresOwnership] Disposable value) { value.Dispose(); }
}");
    }

    [Test]
    public Task Conditional_Reassignment_Remains_Best_Effort()
    {
        return VerifyAsync(@"
public class Test
{
    public void Run(bool condition)
    {
        Disposable d;
        if (condition)
            d = new Disposable();
        else
            d = new Disposable();
        d.Dispose();
    }
}");
    }

    [TestCase("this(value, 0)")]
    [TestCase("base(value)")]
    public Task Constructor_Initializers_Transfer_Ownership(string initializer)
    {
        return VerifyAsync(@"
public class Parent { public Parent([AcquiresOwnership] Disposable value) { value?.Dispose(); } }
public class Owner : Parent
{
    public Owner([AcquiresOwnership] Disposable value) : " + initializer + @" { }
    public Owner([AcquiresOwnership] Disposable value, int unused) : base(value) { }
}");
    }

    [Test]
    public Task Parameter_Reassignment_Does_Not_Erase_An_Acquired_Resource()
    {
        return VerifyAsync(@"
public class Test
{
    public void Consume([AcquiresOwnership] Disposable {|ERP044:value|})
    {
        value = new Disposable();
        value.Dispose();
    }
}");
    }

    [TestCase("var alias = value; alias.Dispose();", false)]
    [TestCase("using var alias = value;", false)]
    [TestCase("var alias = value; using (alias) { }", false)]
    [TestCase("var alias = value; Forward(alias);", false)]
    [TestCase("value = new Disposable(); value.Dispose();", true)]
    public Task Infers_Disposal_Through_Simple_Callee_Aliases(string body, bool leaks)
    {
        return VerifyAsync(@"
public class Test
{
    public void Run() { Consume(" + (leaks ? "{|ERP044:new Disposable()|}" : "new Disposable()") + @"); }
    private static void Consume(Disposable value) { " + body + @" }
    private static void Forward([AcquiresOwnership] Disposable value) { value.Dispose(); }
}");
    }

    [TestCase("flag ? ConsumeAndReturn(d) : null")]
    [TestCase("flag ? d : null", true)]
    public Task Conditional_Transfers_Do_Not_Prove_Invalid_Uses(string expression, bool wrap = false)
    {
        return VerifyAsync(@"
public class Test
{
    public void Run(bool flag)
    {
        var d = new Disposable();
        " + (wrap ? "ConsumeAndReturn(" + expression + ");" : "_ = " + expression + ";") + @"
        d.ToString();
    }
    private static object ConsumeAndReturn([AcquiresOwnership] Disposable value) { value?.Dispose(); return null; }
}");
    }

    [TestCase("public void Run() { _owned = {|ERP046:_borrowed|}; }")]
    [TestCase("[ReturnsOwnership] public Disposable Create() => {|ERP046:_borrowed|};")]
    [TestCase("public Disposable Create() => _borrowed;")]
    [TestCase("[KeepsOwnership] public Disposable Borrow() => _borrowed;")]
    [TestCase("public Disposable BorrowedProperty => _borrowed;")]
    [TestCase("[ReturnsOwnership] public Disposable OwnedProperty => {|ERP046:_borrowed|};")]
    [TestCase("public void Create(out Disposable result) { result = {|ERP046:_borrowed|}; }")]
    public Task Borrowed_Resources_Cannot_Escape_As_Owned_Resources(string member)
    {
        return VerifyAsync(@"
public class Test
{
    [NoOwnership] private Disposable _borrowed;
    private Disposable _owned;
    " + member + @"
}");
    }

    [TestCase("using ({|ERP046:value|}) { }")]
    [TestCase("using (var alias = {|ERP046:value|}) { }")]
    [TestCase("using var alias = {|ERP046:value|};")]
    [TestCase("using var alias = {|ERP046:Borrow()|};")]
    public Task Using_Cannot_Dispose_Explicitly_Borrowed_Resources(string body)
    {
        return VerifyAsync(@"
public class Test
{
    public void Run([NoOwnership] Disposable value) { " + body + @" }
    [KeepsOwnership] private static Disposable Borrow() => null;
}");
    }

    [TestCase("resource.Borrow()")]
    [TestCase("resource.BorrowGeneric()")]
    public Task Explicit_Borrowed_Results_Do_Not_Imply_Receiver_Identity(string expression)
    {
        return VerifyAsync(@"
public class Resource : System.IDisposable
{
    public void Dispose() { }
    [KeepsOwnership] public Resource Borrow() => null;
}
public static class BorrowExtensions
{
    [KeepsOwnership] public static T BorrowGeneric<T>(this T resource) => default;
}
public class Test
{
    public void Run()
    {
        var {|ERP044:resource|} = new Resource();
        var borrowed = " + expression + @";
        {|ERP046:borrowed|}.Dispose();
    }
}");
    }

    [TestCase("new System.IO.StreamReader(stream)", false)]
    [TestCase("new System.IO.StreamReader(stream, System.Text.Encoding.UTF8, true, 1024, false)", false)]
    [TestCase("new System.IO.StreamReader(stream, System.Text.Encoding.UTF8, true, 1024, true)", true)]
    [TestCase("new System.IO.StreamReader(stream, System.Text.Encoding.UTF8, true, 1024, leaveOpen)", true)]
    public Task Source_Forwarding_Uses_The_Same_Stream_Wrapper_Policy(string creation, bool retainsStream)
    {
        return VerifyAsync(@"
public class Test
{
    public void Run(bool leaveOpen)
    {
        var " + (retainsStream ? "{|ERP044:stream|}" : "stream") + @" =
            new System.IO.FileStream(""path"", System.IO.FileMode.Open);
        using var reader = Read(stream, leaveOpen);
    }
    private static System.IO.StreamReader Read(System.IO.Stream stream, bool leaveOpen) => " + creation + @";
}");
    }

    [TestCase("var alias = flag ? d : other; d.Dispose(); alias.ToString();")]
    [TestCase("var alias = flag ? d : other; alias.Dispose(); d.ToString();")]
    [TestCase("var alias = flag ? d : other; var copy = alias; d.Dispose(); copy.ToString();")]
    [TestCase("var alias = flag ? d : other; alias = d; d.Dispose(); {|ERP046:alias|}.ToString();")]
    [TestCase("var alias = d; if (flag) alias = other; d.Dispose(); alias.ToString();")]
    [TestCase("var alias = d; if (flag) alias = other; d.Dispose(); alias.ToString(); {|ERP046:d|}.ToString();")]
    [TestCase("d.Dispose(); var alias = flag ? d : other; alias.ToString();")]
    [TestCase("d.Dispose(); var name = nameof(d);")]
    [TestCase("d.Dispose(); var name = nameof(d.Dispose);")]
    public Task Invalid_Use_Requires_A_Definite_Runtime_Alias(string body)
    {
        return VerifyAsync(@"
public class Test
{
    public void Run(bool flag, Disposable other)
    {
        var d = new Disposable();
        " + body + @"
    }
}");
    }

    [Test]
    public Task Await_Using_Cannot_Dispose_An_Explicitly_Borrowed_Result()
    {
        return VerifyAsync(@"
public class Resource : System.IAsyncDisposable
{
    public System.Threading.Tasks.ValueTask DisposeAsync() => default;
}
public class Test
{
    public async System.Threading.Tasks.Task Run()
    {
        await using var resource = {|ERP046:await Borrow()|};
    }
    [KeepsOwnership] private static System.Threading.Tasks.Task<Resource> Borrow() => null;
}");
    }
}

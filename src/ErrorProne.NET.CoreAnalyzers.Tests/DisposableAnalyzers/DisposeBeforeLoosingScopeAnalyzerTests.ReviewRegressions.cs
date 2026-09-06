using System.Threading.Tasks;
using NUnit.Framework;

namespace ErrorProne.NET.CoreAnalyzers.Tests.DisposableAnalyzers;

public partial class DisposeBeforeLoosingScopeAnalyzerTests
{
    [TestCase("var d = new Disposable(); using (d) { {|ERP044:d|} = new Disposable(); }")]
    [TestCase("var d = new Disposable(); using (var captured = d) { {|ERP044:d|} = new Disposable(); }")]
    [TestCase("using (new Disposable()) { }")]
    [TestCase("Disposable d; using (d = new Disposable()) { }")]
    [TestCase("var d = new Disposable(); using (d) { d = null; }")]
    public Task Using_Captures_The_Original_Owned_Value(string body)
    {
        return VerifyAsync("public class Test { public void Run() { " + body + " } }");
    }

    [TestCase("using (d) { d = null; }")]
    [TestCase("using (var captured = d) { d = null; }")]
    public Task Infers_Using_Consumption_Before_Parameter_Reassignment(string body)
    {
        return VerifyAsync(@"
public class Test
{
    public void Run() { Consume(new Disposable()); }
    private static void Consume(Disposable d) { " + body + @" }
}");
    }

    [TestCase("using (Disposable first = {|ERP046:borrowed|}, second = borrowed = new Disposable()) { }")]
    [TestCase("using Disposable first = {|ERP046:borrowed|}, second = borrowed = new Disposable();")]
    public Task Using_Initializers_Capture_Borrowing_Independently(string body)
    {
        return VerifyAsync("public class Test { public void Run([DoNotDispose] Disposable borrowed) { " + body + " } }");
    }

    [TestCase("var d = new Resource(); await using (d) { {|ERP044:d|} = new Resource(); }")]
    [TestCase("var d = new Resource(); await using (d.ConfigureAwait(false)) { {|ERP044:d|} = new Resource(); }")]
    [TestCase("await using (new Resource()) { }")]
    [TestCase("await using (new Resource().ConfigureAwait(false)) { }")]
    [TestCase("var d = new Resource(); await using (d.ConfigureAwait(false)) { }")]
    [TestCase("var d = new Resource(); await using var captured = d.ConfigureAwait(false);")]
    [TestCase("var d = new Resource(); var configured = d.ConfigureAwait(false); await using (configured) { }")]
    [TestCase("var d = new Resource(); await d.ConfigureAwait(false).DisposeAsync();")]
    [TestCase("var d = new Resource(); await using (TaskAsyncEnumerableExtensions.ConfigureAwait(continueOnCapturedContext: false, source: d)) { }")]
    public Task Configured_Async_Disposal_Preserves_Owned_Identity(string body)
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Resource : System.IAsyncDisposable
{
    public ValueTask DisposeAsync() => default;
}
public class Test { public async Task Run() { " + body + @" } }");
    }

    [TestCase("await using (d) { d = null; }")]
    [TestCase("await using (d.ConfigureAwait(false)) { d = null; }")]
    [TestCase("await using var captured = d.ConfigureAwait(false);")]
    [TestCase("var configured = d.ConfigureAwait(false); await using (configured) { }")]
    public Task Infers_Configured_Async_Disposal_Of_The_Captured_Parameter(string body)
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Resource : System.IAsyncDisposable
{
    public ValueTask DisposeAsync() => default;
}
public class Test
{
    public async Task Run() { await Consume(new Resource()); }
    private static async Task Consume(Resource d) { " + body + @" }
}");
    }

    [TestCase("await using ({|ERP046:d.ConfigureAwait(false)|}) { }")]
    [TestCase("await using var captured = {|ERP046:d.ConfigureAwait(false)|};")]
    [TestCase("var configured = d.ConfigureAwait(false); await using ({|ERP046:configured|}) { }")]
    [TestCase("await {|ERP046:d.ConfigureAwait(false)|}.DisposeAsync();")]
    [TestCase("await using ({|ERP046:d.ConfigureAwait(false)|}) { d = new Resource(); await d.DisposeAsync(); }")]
    public Task Configured_Async_Disposal_Preserves_Borrowed_Identity(string body)
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Resource : System.IAsyncDisposable
{
    public ValueTask DisposeAsync() => default;
}
public class Test { public async Task Run([DoNotDispose] Resource d) { " + body + @" } }");
    }

    [Test]
    public Task Unrelated_ConfigureAwait_Methods_Are_Not_Disposal_Aliases()
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Resource : System.IAsyncDisposable
{
    public ValueTask DisposeAsync() => default;
    public Other ConfigureAwait(bool unused) => new Other();
}
public class Other : System.IAsyncDisposable
{
    public ValueTask DisposeAsync() => default;
}
public class Test
{
    public async Task Run([DoNotDispose] Resource borrowed)
    {
        var {|ERP044:owned|} = new Resource();
        await using (owned.ConfigureAwait(false)) { }
        await using (borrowed.ConfigureAwait(false)) { }
    }
}");
    }

    [TestCase("using var converted = (Other)borrowed;")]
    [TestCase("using var converted = (Other)(dynamic)borrowed;")]
    [TestCase("var {|ERP044:owned|} = new Disposable(); using var converted = (Other)owned;")]
    [TestCase("var {|ERP044:owned|} = new Disposable(); using var converted = (Other)(dynamic)owned;")]
    public Task User_Defined_Conversions_Do_Not_Imply_Resource_Identity(string body)
    {
        return VerifyAsync(@"
public sealed class Other : System.IDisposable
{
    public void Dispose() { }
    public static implicit operator Other(Disposable value) => new Other();
}
public class Test { public void Run([DoNotDispose] Disposable borrowed) { " + body + @" } }");
    }

    [Test]
    public Task Inferring_Consumption_Does_Not_Follow_User_Defined_Conversions()
    {
        return VerifyAsync(@"
public sealed class Other : System.IDisposable
{
    public void Dispose() { }
    public static implicit operator Other(Disposable value) => new Other();
}
public class Test
{
    public void Run() { Consume({|ERP044:new Disposable()|}); }
    private static void Consume(Disposable d) { using var converted = (Other)d; }
}");
    }

    [Test]
    public Task Conversion_Input_And_Result_Contracts_Are_Independent()
    {
        return VerifyAsync(@"
public sealed class Other : System.IDisposable
{
    public void Dispose() { }
    [return: ReturnsOwnership]
    public static implicit operator Other([AcquiresOwnership] Disposable value)
    {
        value.Dispose();
        return new Other();
    }
}
public class Test
{
    public void Run([DoNotDispose] Disposable borrowed)
    {
        var d = new Disposable();
        var {|ERP044:converted|} = (Other)d;
        using var invalid = (Other){|ERP046:borrowed|};
    }
}");
    }

    [Test]
    public Task Borrowed_Conversion_Results_Cannot_Be_Disposed()
    {
        return VerifyAsync(@"
public sealed class Other : System.IDisposable
{
    public void Dispose() { }
    [return: DoNotDispose]
    public static implicit operator Other(Disposable value) => null;
}
public class Test
{
    public void Run(Disposable d) { using var converted = {|ERP046:(Other)d|}; }
}");
    }

    [TestCase("(Other)(Disposable)resource", false)]
    [TestCase("(Other)(Disposable)resource", true)]
    [TestCase("Other.Replace((Disposable)resource)", false)]
    [TestCase("Other.Replace((Disposable)resource)", true)]
    public Task Consuming_Expressions_Update_Aliases_In_Their_Containing_Statement(string expression, bool keepOldAlias)
    {
        return VerifyAsync(@"
public sealed class Other : System.IDisposable
{
    public void Dispose() { }
    [return: ReturnsOwnership]
    public static implicit operator Other([AcquiresOwnership] Disposable value) => Replace(value);
    [return: ReturnsOwnership]
    public static Other Replace([AcquiresOwnership] Disposable value)
    {
        value.Dispose();
        return new Other();
    }
}
public class Test
{
    public void Run()
    {
        System.IDisposable resource = new Disposable();
        " + (keepOldAlias ? "var old = resource;" : "") + @"
        resource = " + expression + @";
        resource.Dispose();
        " + (keepOldAlias ? "{|ERP046:old|}.Dispose();" : "") + @"
    }
}");
    }

    [Test]
    public Task Indexer_Arguments_Honor_Acquisition_And_Borrowing()
    {
        return VerifyAsync(@"
public class Test
{
    public int this[[AcquiresOwnership] Disposable value, Disposable other]
    {
        get { value?.Dispose(); return 0; }
    }
    public void Run([DoNotDispose] Disposable borrowed)
    {
        _ = this[other: null, value: new Disposable()];
        _ = this[other: null, value: {|ERP046:borrowed|}];
        var retained = new Disposable();
        _ = this[other: retained, value: null];
        retained.Dispose();
        Forward(new Disposable());
    }
    private void Forward(Disposable d) { _ = this[other: null, value: d]; }
}");
    }

    [TestCase("borrowed")]
    [TestCase("_borrowed")]
    [TestCase("GetBorrowed()")]
    [TestCase("alias")]
    public Task Borrowed_Values_Cannot_Transfer_Through_Interlocked_Exchange(string value)
    {
        return VerifyAsync(@"
public class Test
{
    private Disposable _owned;
    [DoNotDispose] private Disposable _borrowed;
    [return: DoNotDispose] private static Disposable GetBorrowed() => null;
    public void Run([DoNotDispose] Disposable borrowed)
    {
        var alias = borrowed;
        System.Threading.Interlocked.Exchange(value: {|ERP046:" + value + @"|}, location1: ref _owned);
        System.Threading.Interlocked.Exchange(ref _borrowed, " + value + @");
    }
}");
    }

    [Test]
    public Task Infers_Transfers_Through_Interlocked_Exchange()
    {
        return VerifyAsync(@"
public class Test
{
    private Disposable _owned;
    public void Run() { Store(new Disposable()); }
    private void Store(Disposable value) { System.Threading.Interlocked.Exchange(ref _owned, value); }
}");
    }

    [TestCase("Task", "")]
    [TestCase("Task", ".ConfigureAwait(false)")]
    [TestCase("ValueTask", "")]
    [TestCase("ValueTask", ".ConfigureAwait(false)")]
    public Task Awaited_Properties_Preserve_Explicit_Ownership_Contracts(string taskType, string configure)
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    [ReturnsOwnership] private " + taskType + @"<Disposable> Owned => default;
    [DoNotDispose] private " + taskType + @"<Disposable> Borrowed => default;
    public async Task Run()
    {
        var {|ERP044:owned|} = await Owned" + configure + @";
        using var borrowed = {|ERP046:await Borrowed" + configure + @"|};
        using var cleaned = await Owned" + configure + @";
    }
}");
    }
}

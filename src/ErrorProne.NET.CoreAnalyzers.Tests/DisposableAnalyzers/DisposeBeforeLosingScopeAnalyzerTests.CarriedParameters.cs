using System.Threading.Tasks;
using NUnit.Framework;

namespace ErrorProne.NET.CoreAnalyzers.Tests.DisposableAnalyzers;

public partial class DisposeBeforeLosingScopeAnalyzerTests
{
    [TestCase("Task<Disposable>", "Task.FromResult(item)")]
    [TestCase("ValueTask<Disposable>", "ValueTask.FromResult(item)")]
    [TestCase("ValueTask<Disposable>", "new ValueTask<Disposable>(item)")]
    public Task CarriedParameters_Explicit_Transfer_Preserves_Ownership(string type, string carrier)
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    private object saved;
    private void Take([AcquiresOwnership] " + type + @" pending) { saved = pending; }
    public void Owned()
    {
        var item = new Disposable();
        Take(" + carrier + @");
        {|ERP046:item|}.ToString();
    }
    public void Borrowed([DoNotDispose] Disposable item)
    {
        Take({|ERP046:" + carrier + @"|});
    }
}");
    }

    [TestCase("Task<Disposable>")]
    [TestCase("ValueTask<Disposable>")]
    public Task CarriedParameters_Acquired_Result_Is_Cleaned_Up_By_Awaiting(string type)
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    public async Task Run([AcquiresOwnership] " + type + @" pending)
    {
        var alias = pending;
        var item = await alias.ConfigureAwait(false);
        item.Dispose();
        ({|ERP046:await pending|}).ToString();
    }
}");
    }

    [TestCase("Task<Disposable>", "")]
    [TestCase("Task<Disposable>", "pending.Dispose();")]
    [TestCase("Task<Disposable>", "using (pending) { }")]
    [TestCase("ValueTask<Disposable>", "_ = pending;")]
    public Task CarriedParameters_Wrapper_Only_Cleanup_Does_Not_Satisfy_The_Contract(string type, string body)
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    public void Run([AcquiresOwnership] " + type + @" {|ERP044:pending|})
    {
        " + body + @"
    }
}");
    }

    [TestCase("Task<Disposable>")]
    [TestCase("ValueTask<Disposable>")]
    public Task CarriedParameters_Borrowed_Results_Cannot_Be_Disposed(string type)
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    public async Task Run([DoNotDispose] " + type + @" pending)
    {
        var alias = pending;
        ({|ERP046:await alias.ConfigureAwait(false)|}).Dispose();
    }
}");
    }

    [TestCase("new Sink(Task.FromResult(item))")]
    [TestCase("sink[Task.FromResult(item)]")]
    [TestCase("(Sink)Task.FromResult(item)")]
    public Task CarriedParameters_Other_Acquiring_Boundaries_Check_Carriers(string expression)
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Sink
{
    private object saved;
    public Sink() { }
    public Sink([AcquiresOwnership] Task<Disposable> pending) { saved = pending; }
    public int this[[AcquiresOwnership] Task<Disposable> pending]
    {
        get { saved = pending; return 0; }
    }
    public static implicit operator Sink([AcquiresOwnership] Task<Disposable> pending) => new Sink(pending);
}
public class Test
{
    public void Run(Sink sink, [DoNotDispose] Disposable item)
    {
        _ = " + expression.Replace("Task.FromResult(item)", "{|ERP046:Task.FromResult(item)|}") + @";
    }
    public void Owned(Sink sink)
    {
        var item = new Disposable();
        _ = " + expression + @";
        {|ERP046:item|}.ToString();
    }
}");
    }

    [TestCase("Task<Disposable>", "Task.FromResult(item)")]
    [TestCase("ValueTask<Disposable>", "ValueTask.FromResult(item)")]
    public Task CarriedParameters_Source_Inference_Recognizes_Awaited_Cleanup(string type, string carrier)
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    private static async Task Cleanup(" + type + @" pending) { (await pending).Dispose(); }
    public async Task Run([DoNotDispose] Disposable item)
    {
        await Cleanup({|ERP046:" + carrier + @"|});
    }
    public async Task Owned()
    {
        var item = new Disposable();
        await Cleanup(" + carrier + @");
        {|ERP046:item|}.ToString();
    }
}");
    }

    [Test]
    public Task CarriedParameters_Source_Inference_Follows_Wrapped_Arguments()
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    private static object saved;
    private static void Take([AcquiresOwnership] Task<Disposable> pending) { saved = pending; }
    private static void Forward(Disposable value) { Take(Task.FromResult(value)); }
    public void Run([DoNotDispose] Disposable item) { Forward({|ERP046:item|}); }
    public void Owned()
    {
        var item = new Disposable();
        Forward(item);
        {|ERP046:item|}.ToString();
    }
}");
    }

    [Test]
    public Task CarriedParameters_Disposing_Only_The_Wrapper_Does_Not_Infer_Result_Acquisition()
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    private static void WrapperOnly(Task<Disposable> pending) { pending.Dispose(); }
    public void Run([DoNotDispose] Disposable item) { WrapperOnly(Task.FromResult(item)); }
    public async Task Owned([AcquiresOwnership] Task<Disposable> pending)
    {
        (await pending).Dispose();
        pending.Dispose();
    }
}");
    }

    [TestCase("Task<Disposable>")]
    [TestCase("ValueTask<Disposable>")]
    public Task CarriedParameters_Capture_Remains_An_Unknown_Handoff(string type)
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    public System.Action Run([AcquiresOwnership] " + type + @" pending)
        => () => System.GC.KeepAlive(pending);
}");
    }

    [Test]
    public Task CarriedParameters_Borrowed_Reassignment_Updates_Result_Binding()
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    public async Task Replace([DoNotDispose] Task<Disposable> pending)
    {
        pending = Task.FromResult(new Disposable());
        (await pending).Dispose();
    }
    public async Task Preserve([DoNotDispose] Task<Disposable> pending, [DoNotDispose] Disposable item, bool replace)
    {
        if (replace) { pending = Task.FromResult(item); }
        ({|ERP046:await pending|}).Dispose();
    }
}");
    }
}

using System.Threading.Tasks;
using NUnit.Framework;

namespace ErrorProne.NET.CoreAnalyzers.Tests.DisposableAnalyzers;

public partial class DisposeBeforeLosingScopeAnalyzerTests
{
    [TestCase("registry.Register(item);")]
    [TestCase("registry.Identity(item);")]
    public Task LowNoiseReview_Fluent_Receivers_Do_Not_Hide_Other_Unknown_Arguments(string body)
    {
        return VerifyAsync(@"
public interface IRegistry { IRegistry Register(Disposable value); }
public static class Extensions { public static T Identity<T>(this T value, Disposable other) => value; }
public class Test
{
    public void Run(IRegistry registry)
    {
        var item = new Disposable();
        " + body + @"
        item.ToString();
    }
}");
    }

    [TestCase("var item = new Disposable();", "using var result = await alias;")]
    [TestCase("var {|ERP044:item|} = new Disposable();", "alias.Dispose();")]
    public Task LowNoiseReview_Fluent_Carrier_Identity_Is_Not_Resource_Identity(string creation, string cleanup)
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public static class Extensions { public static T Identity<T>(this T value) => value; }
public class Test
{
    public async Task Run()
    {
        " + creation + @"
        var pending = Task.FromResult(item);
        var alias = pending.Identity();
        " + cleanup + @"
        await Task.CompletedTask;
    }
}");
    }

    [Test]
    public Task LowNoiseReview_Fluent_Carriers_Preserve_Borrowing()
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public static class Extensions { public static T Identity<T>(this T value) => value; }
public class Test
{
    public async Task Run([DoNotDispose] Disposable item)
    {
        var alias = Task.FromResult(item).Identity();
        using var result = {|ERP046:await alias|};
    }
}");
    }

    [TestCase("System.Action cleanup = () => item.Dispose();")]
    [TestCase("void cleanup() { item.Dispose(); }")]
    public Task LowNoiseReview_Capture_Before_Acquisition_Is_Unknown(string callback)
    {
        return VerifyAsync(@"
public class Test
{
    public void Run()
    {
        Disposable item = null;
        " + callback + @"
        item = new Disposable();
        cleanup();
    }
}");
    }

    [Test]
    public Task LowNoiseReview_Nameof_Before_Acquisition_Does_Not_Capture()
    {
        return VerifyAsync(@"
public class Test
{
    public void Run()
    {
        Disposable item = null;
        System.Action callback = () => System.Console.WriteLine(nameof(item));
        {|ERP044:item|} = new Disposable();
        callback();
    }
}");
    }

    [TestCase("_ = this + item;")]
    [TestCase("dynamic target = this; _ = target[item];")]
    public Task LowNoiseReview_Operator_And_Dynamic_Indexer_Arguments_Are_Handoffs(string handoff)
    {
        return VerifyAsync(@"
public class Test
{
    public static Test operator +(Test target, Disposable value) => target;
    public void Run()
    {
        var item = new Disposable();
        " + handoff + @"
        item.ToString();
    }
}");
    }

    [Test]
    public Task LowNoiseReview_Operator_Input_Contracts_Are_Independent()
    {
        return VerifyAsync(@"
public class Test
{
    public static int operator +(Test target, [DoNotDispose] Disposable value) => 0;
    public static int operator -(Test target, [AcquiresOwnership] Disposable value) { value.Dispose(); return 0; }
    public void Run([DoNotDispose] Disposable borrowed)
    {
        var {|ERP044:item|} = new Disposable();
        _ = this + item;
        _ = this - {|ERP046:borrowed|};
    }
}");
    }

    [TestCase("_ = +item;")]
    [TestCase("item++;")]
    [TestCase("item += 1;")]
    public Task LowNoiseReview_Unannotated_Unary_And_Compound_Operators_Are_Handoffs(string body)
    {
        return VerifyAsync(@"
public class Resource : System.IDisposable
{
    public void Dispose() { }
    public static int operator +(Resource value) => 0;
    public static Resource operator ++(Resource value) => value;
    public static Resource operator +(Resource value, int count) => value;
}
public class Test
{
    public void Run()
    {
        var item = new Resource();
        " + body + @"
    }
}");
    }

    [TestCase("Interlocked.Exchange(ref pending, {|ERP046:Task.FromResult(item)|});", true)]
    [TestCase("Interlocked.Exchange(ref pending, Task.FromResult(item)); {|ERP046:item|}.Dispose();", false)]
    public Task LowNoiseReview_Interlocked_Carriers_Respect_Ownership(string body, bool borrowed)
    {
        return VerifyAsync(@"
using System.Threading;
using System.Threading.Tasks;
public class Test
{
    private Task<Disposable> pending;
    public void Run(" + (borrowed ? "[DoNotDispose] Disposable item" : "") + @")
    {
        " + (borrowed ? "" : "var item = new Disposable();") + @"
        " + body + @"
    }
}");
    }

    [TestCase("(await Task.FromResult(value)).Dispose();", true)]
    [TestCase("var pending = Task.FromResult(value); using var result = await pending.ConfigureAwait(false);", true)]
    [TestCase("var pending = Task.FromResult(value); var alias = pending; pending = null; (await alias).Dispose();", true)]
    [TestCase("var pending = Task.FromResult(value); pending = Task.FromResult<Disposable>(null); (await pending).Dispose();", false)]
    [TestCase("var pending = Task.FromResult(value); pending.Dispose(); await Task.CompletedTask;", false)]
    [TestCase("_ = Task.FromResult(value); await Task.CompletedTask;", false)]
    [TestCase("var pending = Task.FromResult(value); Unknown(pending); await Task.CompletedTask;", false)]
    [TestCase("var pending = new ValueTask<Disposable>(value); (await pending).Dispose();", true)]
    [TestCase("var pending = ValueTask.FromResult(value); (await pending).Dispose();", true)]
    public Task LowNoiseReview_Source_Consumption_Follows_Completed_Carriers_Only_When_Consumed(string body, bool consumes)
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    public Task Run([DoNotDispose] Disposable borrowed) => Consume(" +
            (consumes ? "{|ERP046:borrowed|}" : "borrowed") + @");
    private static async Task Consume(Disposable value) { " + body + @" }
    private static void Unknown(object pending) { }
}");
    }
}

using System.Threading.Tasks;
using NUnit.Framework;

namespace ErrorProne.NET.CoreAnalyzers.Tests.DisposableAnalyzers;

public partial class DisposeBeforeLosingScopeAnalyzerTests
{
    // These characterize v1 boundaries, including known noise and missed obligations.
    // A passing test here does not mean the example has a safe resource lifetime.
    [Test]
    public Task Adoption_Task_Publication_Transfers_The_Carried_Resource()
    {
        return VerifyAsync(@"
public class Test
{
    [return: ReturnsOwnership]
    private static System.Threading.Tasks.Task<Disposable> CreateAsync()
    {
        var item = new Disposable();
        return System.Threading.Tasks.Task.FromResult(item);
    }

    public async System.Threading.Tasks.Task RunAsync()
    {
        using var item = await CreateAsync();
    }
}");
    }

    [Test]
    public Task Adoption_Unannotated_Collection_Makes_Element_Ownership_Unknown()
    {
        return VerifyAsync(@"
public sealed class Test : System.IDisposable
{
    private readonly System.Collections.Generic.List<Disposable> items = new();

    public void Add()
    {
        var item = new Disposable();
        items.Add(item);
    }

    public void Dispose()
    {
        foreach (var item in items)
            item.Dispose();
        items.Clear();
    }
}");
    }

    [Test]
    public Task Adoption_Shutdown_Capture_Makes_Local_Ownership_Unknown()
    {
        return VerifyAsync(@"
public class Test
{
    public void Register()
    {
        var item = new Disposable();
        System.AppDomain.CurrentDomain.ProcessExit += (sender, args) => item.Dispose();
    }
}");
    }

    [Test]
    public Task Adoption_Lambda_Local_Leak_Currently_Is_Not_Reported()
    {
        return VerifyAsync(@"
public class Test
{
    public void Run()
    {
        System.Action callback = () =>
        {
            var item = new Disposable();
            item.ToString();
        };
        callback();
    }
}");
    }

    [Test]
    public Task Adoption_Conditional_Cleanup_Currently_Discharges_All_Paths()
    {
        return VerifyAsync(@"
public class Test
{
    public void Run(bool shouldDispose)
    {
        var item = new Disposable();
        if (shouldDispose)
            item.Dispose();
    }
}");
    }

    [Test]
    public Task Adoption_Member_Transfer_Currently_Does_Not_Require_Owner_Teardown()
    {
        return VerifyAsync(@"
public sealed class Test : System.IDisposable
{
    private readonly Disposable item;

    public Test()
    {
        item = new Disposable();
    }

    public void Dispose() { }
}");
    }

    [Test]
    public Task Adoption_Discarded_Async_Cleanup_Currently_Discharges_Ownership()
    {
        return VerifyAsync(@"
public sealed class AsyncResource : System.IAsyncDisposable
{
    public async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        await System.Threading.Tasks.Task.Yield();
    }
}

public class Test
{
    public void Run()
    {
        var item = new AsyncResource();
        _ = item.DisposeAsync();
    }
}");
    }

    [TestCase("var {|ERP044:item|} = Create();")]
    [TestCase("using var item = Create();")]
    [TestCase("Consume(Create());")]
    public Task Adoption_Explicit_Owned_Result_Respects_Cleanup_And_Transfer(string body)
    {
        return VerifyAsync(@"
public class Test
{
    [return: ReturnsOwnership]
    private static Disposable Create() => new Disposable();

    private static void Consume([AcquiresOwnership] Disposable item) => item.Dispose();

    public void Run() { " + body + @" }
}");
    }
}

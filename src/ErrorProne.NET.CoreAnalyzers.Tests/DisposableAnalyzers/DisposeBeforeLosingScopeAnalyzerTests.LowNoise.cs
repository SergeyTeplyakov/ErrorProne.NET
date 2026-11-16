using System.Threading.Tasks;
using NUnit.Framework;

namespace ErrorProne.NET.CoreAnalyzers.Tests.DisposableAnalyzers;

public partial class DisposeBeforeLosingScopeAnalyzerTests
{
    [TestCase("Unknown(item); item.ToString();")]
    [TestCase("_ = new Holder(item);")]
    [TestCase("_ = this[item];")]
    [TestCase("var alias = item; Unknown(alias);")]
    [TestCase("System.Action callback = () => item.ToString();")]
    [TestCase("System.Action callback = item.Dispose;")]
    [TestCase("void Callback() { item.ToString(); }")]
    [TestCase("var pending = Task.FromResult(item); Unknown(pending);")]
    [TestCase("dynamic target = this; target.Unknown(item);")]
    public Task LowNoise_Unknown_Handoffs_And_Captures_Are_Not_Leaks_Or_Definite_Moves(string body)
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    public void Run()
    {
        var item = new Disposable();
        " + body + @"
        item.ToString();
    }
    public static void Unknown(object value) { }
    public int this[Disposable value] => 0;
}
public class Holder { public Holder(Disposable value) { } }
");
    }

    [TestCase("item.ToString();")]
    [TestCase("_ = item;")]
    [TestCase("Borrow(item);")]
    [TestCase("_ = new Borrower(item);")]
    [TestCase("System.Action callback = () => System.Console.WriteLine(nameof(item));")]
    [TestCase("System.Action callback = () => { var other = new Disposable(); other.Dispose(); };")]
    [TestCase("var alias = item.ThrowIfNull();")]
    public Task LowNoise_Receivers_Borrowing_And_NonCaptures_Keep_The_Obligation(string body)
    {
        return VerifyAsync(@"
public class Test
{
    public void Run()
    {
        var {|ERP044:item|} = new Disposable();
        " + body + @"
    }
    public static void Borrow([DoNotDispose] Disposable value) { }
}
public class Borrower { public Borrower([DoNotDispose] Disposable value) { } }
");
    }

    [TestCase("Unknown(item);")]
    [TestCase("System.Action callback = () => item.ToString();")]
    public Task LowNoise_Uncertainty_Does_Not_Hide_Later_Definite_Misuse(string handoff)
    {
        return VerifyAsync(@"
public class Test
{
    public void Run()
    {
        var item = new Disposable();
        " + handoff + @"
        item.Dispose();
        {|ERP046:item|}.ToString();
    }
    private static void Unknown(object value) { }
}");
    }

    [Test]
    public Task LowNoise_Unknown_Consumption_Is_Not_Inferred_As_Acquiring()
    {
        return VerifyAsync(@"
public class Test
{
    public void Run([DoNotDispose] Disposable borrowed)
    {
        Forward(borrowed);
        borrowed.ToString();
    }
    private static void Forward(Disposable value) { Unknown(value); }
    private static void Unknown(object value) { }
}");
    }

    [TestCase("Task.FromResult(item)")]
    [TestCase("new ValueTask<Disposable>(item)")]
    [TestCase("ValueTask.FromResult(item)")]
    public Task LowNoise_Discarding_A_Completed_Task_Keeps_The_Resource_Obligation(string expression)
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    public void Run()
    {
        var {|ERP044:item|} = new Disposable();
        _ = " + expression + @";
    }
}");
    }

    [TestCase("var pending = Task.FromResult(item); pending.Dispose();")]
    [TestCase("using var pending = Task.FromResult(item);")]
    [TestCase("var pending = Task.FromResult(item); var alias = pending; _ = alias;")]
    [TestCase("var pending = Task.FromResult(item); pending = Task.FromResult<Disposable>(null); using var value = await pending;")]
    public Task LowNoise_Task_Lifetime_Is_Not_Its_Result_Lifetime(string body)
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    public async Task Run()
    {
        var {|ERP044:item|} = new Disposable();
        " + body + @"
        await Task.CompletedTask;
    }
}");
    }

    [TestCase("using var value = await Task.FromResult(item);")]
    [TestCase("using var value = await Task.FromResult(item).ConfigureAwait(false);")]
    [TestCase("using var value = await new ValueTask<Disposable>(item);")]
    [TestCase("using var value = await ValueTask.FromResult(item);")]
    [TestCase("var pending = Task.FromResult(item); var alias = pending; pending = null; using var value = await alias;")]
    [TestCase("var pending = Task.FromResult(item).ConfigureAwait(false); using var value = await pending;")]
    [TestCase("var pending = Task.FromResult(item); var value = await pending; value.Dispose();")]
    public Task LowNoise_Completed_Task_Results_Retain_Resource_Identity(string body)
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    public async Task Run()
    {
        var item = new Disposable();
        " + body + @"
    }
}");
    }

    [Test]
    public Task LowNoise_Completed_Task_Result_Reports_Use_After_Disposal()
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    public async Task Run()
    {
        var item = new Disposable();
        var pending = Task.FromResult(item);
        var value = await pending;
        value.Dispose();
        {|ERP046:item|}.ToString();
    }
}");
    }

    [TestCase("using var value = {|ERP046:await Task.FromResult(item)|};")]
    [TestCase("var pending = Task.FromResult(item); using var value = {|ERP046:await pending|};")]
    [TestCase("var pending = new ValueTask<Disposable>(item); using var value = {|ERP046:await pending.ConfigureAwait(false)|};")]
    [TestCase("var pending = Task.FromResult(item); var value = await pending; {|ERP046:value|}.Dispose();")]
    [TestCase("using var pending = Task.FromResult(item);")]
    [TestCase("var pending = Task.FromResult(item); pending = Task.FromResult(new Disposable()); using var value = await pending;")]
    public Task LowNoise_Completed_Task_Results_Preserve_Borrowing_Not_Task_Borrowing(string body)
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    public async Task Run([DoNotDispose] Disposable item)
    {
        " + body + @"
        await Task.CompletedTask;
    }
}");
    }

    [Test]
    public Task LowNoise_Owning_Task_Return_Cannot_Carry_A_Borrowed_Resource()
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    [return: ReturnsOwnership]
    public Task<Disposable> Wrap([DoNotDispose] Disposable borrowed)
    {
        var pending = Task.FromResult(borrowed);
        return {|ERP046:pending|};
    }
}");
    }

    [Test]
    public Task LowNoise_Unknown_Handoff_Does_Not_Hide_A_Replacement_Lifetime()
    {
        return VerifyAsync(@"
public class Test
{
    public void Run()
    {
        var item = new Disposable();
        Unknown(item);
        {|ERP044:item|} = new Disposable();
    }
    private static void Unknown(object value) { }
}");
    }

    [Test]
    public Task LowNoise_Acquired_Parameter_May_Be_Handed_To_An_Unknown_Consumer()
    {
        return VerifyAsync(@"
public class Test
{
    public void Forward([AcquiresOwnership] Disposable item) { Unknown(item); }
    private static void Unknown(object value) { }
}");
    }

    [Test]
    public Task LowNoise_Explicit_Borrowing_Survives_An_Unknown_Handoff()
    {
        return VerifyAsync(@"
public class Test
{
    public void Run([DoNotDispose] Disposable item)
    {
        Unknown(item);
        {|ERP046:item|}.Dispose();
    }
    private static void Unknown(object value) { }
}");
    }

    [TestCase("Task.FromResult(new Disposable())", "Task<Disposable>")]
    [TestCase("new ValueTask<Disposable>(new Disposable())", "ValueTask<Disposable>")]
    [TestCase("ValueTask.FromResult(new Disposable())", "ValueTask<Disposable>")]
    public Task LowNoise_Completed_Wrappers_Can_Be_Returned_Through_A_Local(string value, string type)
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    [return: ReturnsOwnership]
    public " + type + @" Create()
    {
        var pending = " + value + @";
        return pending;
    }
}");
    }

    [TestCase("Task.FromResult(item)")]
    [TestCase("new ValueTask<Disposable>(item)")]
    public Task LowNoise_Awaiting_Without_Cleanup_Does_Not_Discharge_A_Wrapped_Resource(string value)
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    public async Task Run()
    {
        var {|ERP044:item|} = new Disposable();
        var result = await " + value + @";
        result.ToString();
    }
}");
    }

    [Test]
    public Task LowNoise_UserDefined_Conversion_Is_An_Unknown_Handoff_Not_An_Alias()
    {
        return VerifyAsync(@"
public sealed class Other : System.IDisposable
{
    public void Dispose() { }
    public static implicit operator Other(Disposable input) => new Other();
}
public class Test
{
    public void Run()
    {
        var input = new Disposable();
        using var output = (Other)input;
        input.ToString();
    }
}");
    }

    [Test]
    public Task LowNoise_Awaiting_A_Known_Disposed_Resource_Is_Still_Misuse()
    {
        return VerifyAsync(@"
using System.Threading.Tasks;
public class Test
{
    public async Task Run()
    {
        var item = new Disposable();
        var pending = Task.FromResult(item);
        item.Dispose();
        var value = {|ERP046:await pending|};
    }
}");
    }
}

using System.Threading.Tasks;
using NUnit.Framework;

namespace ErrorProne.NET.CoreAnalyzers.Tests.DisposableAnalyzers;

public partial class DisposeBeforeLosingScopeAnalyzerTests
{
    [TestCase("int DisposeAsync() => 0")]
    [TestCase("void DisposeAsync() => System.GC.KeepAlive(null)")]
    [TestCase("System.Threading.Tasks.Task DisposeAsync() => System.Threading.Tasks.Task.CompletedTask")]
    [TestCase("System.Threading.Tasks.ValueTask DisposeAsync() => default")]
    public Task PrFeedback_Unrelated_DisposeAsync_Does_Not_End_Ownership(string method)
    {
        return VerifyAsync(@"
public class Resource : System.IDisposable
{
    public void Dispose() { }
    public " + method + @";
}
public class Test
{
    public void Run()
    {
        var {|ERP044:resource|} = new Resource();
        resource.DisposeAsync();
        resource.ToString();
    }
}");
    }

    [TestCase("int", "0")]
    [TestCase("System.Threading.Tasks.ValueTask", "default")]
    public Task PrFeedback_Public_Lookalike_Is_Not_An_Explicit_Async_Implementation(string returnType, string result)
    {
        return VerifyAsync(@"
public class Resource : System.IAsyncDisposable
{
    System.Threading.Tasks.ValueTask System.IAsyncDisposable.DisposeAsync() => default;
    public " + returnType + " DisposeAsync() => " + result + @";
}
public class Test
{
    public void Run()
    {
        var {|ERP044:resource|} = new Resource();
        resource.DisposeAsync();
        resource.ToString();
    }
}");
    }

    [Test]
    public Task PrFeedback_Lookalike_Is_Not_Borrowed_Cleanup_Or_Inferred_Acquisition()
    {
        return VerifyAsync(@"
public class Resource : System.IDisposable
{
    public void Dispose() { }
    public int DisposeAsync() => 0;
}
public class Test
{
    private static void Inspect(Resource resource) { resource.DisposeAsync(); }
    public void Run([DoNotDispose] Resource resource)
    {
        resource.DisposeAsync();
        Inspect(resource);
        resource.ToString();
    }
}");
    }

    [TestCase("resource.DisposeAsync();")]
    [TestCase("((System.IAsyncDisposable)resource).DisposeAsync();")]
    [TestCase("System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(resource, false).DisposeAsync();")]
    public Task PrFeedback_Actual_Async_Disposal_Still_Discharges_Ownership(string cleanup)
    {
        return VerifyAsync(@"
public class Resource : System.IAsyncDisposable
{
    public System.Threading.Tasks.ValueTask DisposeAsync() => default;
}
public class Test
{
    public void Run()
    {
        var resource = new Resource();
        " + cleanup + @"
    }
}");
    }

    [TestCase("")]
    [TestCase("public override System.Threading.Tasks.ValueTask DisposeAsync() => base.DisposeAsync();")]
    public Task PrFeedback_Inherited_And_Overridden_Async_Disposal_Are_Recognized(string implementation)
    {
        return VerifyAsync(@"
public class Base : System.IAsyncDisposable
{
    public virtual System.Threading.Tasks.ValueTask DisposeAsync() => default;
}
public class Resource : Base { " + implementation + @" }
public class Test
{
    public void Run()
    {
        var resource = new Resource();
        resource.DisposeAsync();
    }
}");
    }

    [Test]
    public Task PrFeedback_Reimplemented_Async_Disposal_Does_Not_Trust_An_Inherited_Lookalike()
    {
        return VerifyAsync(@"
public class Base : System.IAsyncDisposable
{
    public System.Threading.Tasks.ValueTask DisposeAsync() => default;
}
public class Resource : Base, System.IAsyncDisposable
{
    System.Threading.Tasks.ValueTask System.IAsyncDisposable.DisposeAsync() => default;
}
public class Test
{
    public void Run()
    {
        var {|ERP044:resource|} = new Resource();
        resource.DisposeAsync();
    }
}");
    }

    [Test]
    public Task PrFeedback_Generic_DisposeAsync_Lookalike_Does_Not_End_Ownership()
    {
        return VerifyAsync(@"
public class Resource : System.IAsyncDisposable
{
    public System.Threading.Tasks.ValueTask DisposeAsync() => default;
    public System.Threading.Tasks.ValueTask DisposeAsync<T>() => default;
}
public class Test
{
    public void Run()
    {
        var {|ERP044:resource|} = new Resource();
        resource.DisposeAsync<int>();
    }
}");
    }

    [TestCase("object", "")]
    [TestCase("T", "<T>")]
    public Task PrFeedback_Explicit_Acquisition_Requires_Callee_Cleanup_For_Erased_Types(string type, string typeParameters)
    {
        return VerifyAsync(@"
public class Test
{
    private static void Take" + typeParameters + "([AcquiresOwnership] " + type + @" {|ERP044:value|}) { }
    public void Run()
    {
        Take(new Disposable());
    }
}");
    }

    [TestCase("object", "", "((System.IDisposable)value).Dispose();")]
    [TestCase("T", "<T>", "(value as System.IDisposable)?.Dispose();")]
    [TestCase("object", "", "if (value is System.IDisposable resource) { resource.Dispose(); }")]
    [TestCase("T", "<T>", "if (value is not System.IDisposable resource) return; resource.Dispose();")]
    public Task PrFeedback_Erased_Acquiring_Parameters_Can_Be_Disposed(string type, string typeParameters, string cleanup)
    {
        return VerifyAsync(@"
public class Test
{
    private static void Take" + typeParameters + "([AcquiresOwnership] " + type + @" value)
    {
        " + cleanup + @"
    }
    public void Run()
    {
        Take(new Disposable());
    }
}");
    }

    [Test]
    public Task PrFeedback_Pattern_Matching_Alone_Does_Not_Discharge_An_Acquiring_Parameter()
    {
        return VerifyAsync(@"
public class Test
{
    public void Take<T>([AcquiresOwnership] T {|ERP044:value|})
    {
        if (value is System.IDisposable resource) { resource.ToString(); }
    }
}");
    }

    [Test]
    public Task PrFeedback_Pattern_Aliases_Preserve_Borrowing()
    {
        return VerifyAsync(@"
public class Test
{
    public void Run([DoNotDispose] object value)
    {
        if (value is System.IDisposable resource) { {|ERP046:resource|}.Dispose(); }
    }
}");
    }

    [TestCase("Dispose", "{|ERP046:value|}")]
    [TestCase("ToString", "value")]
    public Task PrFeedback_Pattern_Based_Source_Acquisition_Requires_Actual_Cleanup(string method, string argument)
    {
        return VerifyAsync(@"
public class Test
{
    private static void Inspect(object value)
    {
        if (value is System.IDisposable resource) { resource." + method + @"(); }
    }
    public void Run([DoNotDispose] Disposable value) { Inspect(" + argument + @"); }
}");
    }

    [TestCase("object", "")]
    [TestCase("T", "<T>")]
    public Task PrFeedback_Erased_Acquiring_Parameters_Can_Transfer_To_Storage(string type, string typeParameters)
    {
        return VerifyAsync(@"
public class Test
{
    private object stored;
    public void Take" + typeParameters + "([AcquiresOwnership] " + type + @" value)
    {
        stored = value;
    }
}");
    }
}

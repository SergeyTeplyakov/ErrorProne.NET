using System.Threading.Tasks;
using NUnit.Framework;

namespace ErrorProne.NET.CoreAnalyzers.Tests.DisposableAnalyzers;

public partial class DisposeBeforeLoosingScopeAnalyzerTests
{
    [TestCase("[return: DoNotDispose]")]
    [TestCase("[DoNotDispose]")]
    [TestCase("[KeepsOwnership]")]
    public Task Borrowed_Returns_Do_Not_Cancel_Acquired_Input_Obligations(string attribute)
    {
        return VerifyAsync(@"
public class Test
{
    public void Run([DoNotDispose] Disposable borrowed)
    {
        var owned = new Disposable();
        var result = BorrowOther(owned, borrowed);
    }

    " + attribute + @"
    private static Disposable BorrowOther(
        [AcquiresOwnership] Disposable {|ERP044:owned|},
        [DoNotDispose] Disposable borrowed) => borrowed;

    " + attribute + @"
    private static Disposable DisposeAndBorrow(
        [AcquiresOwnership] Disposable owned,
        [DoNotDispose] Disposable borrowed)
    {
        owned.Dispose();
        return borrowed;
    }

    " + attribute + @"
    private static Disposable ReturnTheInput([AcquiresOwnership] Disposable owned) => owned;

    " + attribute + @"
    private static Disposable BorrowOnly([DoNotDispose] Disposable borrowed) => borrowed;
}");
    }

    [Test]
    public Task Borrowed_Returns_Still_Check_Each_Acquired_Input()
    {
        return VerifyAsync(@"
public class Test
{
    [return: DoNotDispose]
    public Disposable BorrowOther(
        [AcquiresOwnership] Disposable first,
        [AcquiresOwnership] Disposable {|ERP044:second|},
        [DoNotDispose] Disposable borrowed)
    {
        first.Dispose();
        return borrowed;
    }
}");
    }

    [TestCase("{|ERP046:GetShared()|}.Dispose();")]
    [TestCase("var resource = GetShared(); {|ERP046:resource|}.Dispose();")]
    [TestCase("var resource = GetShared(); {|ERP046:resource|}?.Dispose();")]
    [TestCase("var resource = GetShared(); var alias = resource; {|ERP046:alias|}.Close();")]
    [TestCase("using var resource = {|ERP046:GetShared()|};")]
    [TestCase("using (var resource = {|ERP046:GetShared()|}) { }")]
    [TestCase("var resource = GetShared(); using ({|ERP046:resource|}) { }")]
    [TestCase("var resource = GetShared(); using var alias = {|ERP046:resource|};")]
    [TestCase("var resource = GetShared(); Consume({|ERP046:resource|});")]
    [TestCase("var resource = GetShared(); resource.ToString();")]
    [TestCase("var resource = GetShared(); resource = new Disposable(); resource.Dispose();")]
    [TestCase("var resource = GetShared(); var alias = resource; resource = new Disposable(); resource.Dispose(); {|ERP046:alias|}.Dispose();")]
    [TestCase("var resource = new Disposable(); resource.Dispose(); resource = GetShared(); {|ERP046:resource|}.Dispose();")]
    [TestCase("if (condition) { var resource = GetShared(); {|ERP046:resource|}.Dispose(); }")]
    [TestCase("var resource = GetShared(); if (condition) resource = new Disposable(); resource.Dispose();")]
    [TestCase("var resource = GetShared(); using ({|ERP046:resource|}) { resource = new Disposable(); resource.Dispose(); }")]
    [TestCase("var resource = condition ? GetShared() : GetShared(); {|ERP046:resource|}.Dispose();")]
    public Task DoNotDispose_Returns_Are_Tracked_Through_Simple_Aliases(string body)
    {
        return VerifyAsync(@"
public class Test
{
    public void Run(bool condition) { " + body + @" }
    [return: DoNotDispose] private static Disposable GetShared() => null;
    private static void Consume([AcquiresOwnership] Disposable resource) { resource.Dispose(); }
}");
    }

    [TestCase("parameter")]
    [TestCase("_field")]
    [TestCase("Property")]
    [TestCase("GetterProperty")]
    public Task DoNotDispose_Parameters_And_Members_Cannot_Be_Disposed(string expression)
    {
        return VerifyAsync(@"
public class Test
{
    [DoNotDispose] private Disposable _field;
    [DoNotDispose] private Disposable Property => null;
    private Disposable GetterProperty { [return: DoNotDispose] get => null; }
    public void Run([DoNotDispose] Disposable parameter)
    {
        var alias = " + expression + @";
        {|ERP046:alias|}.Dispose();
    }
}");
    }

    [TestCase("DoNotDispose")]
    [TestCase("NoOwnership")]
    public Task Borrowed_Parameter_Reassignment_Starts_A_New_Owned_Value(string attribute)
    {
        return VerifyAsync(@"
public class Test
{
    public void Run([" + attribute + @"] Disposable parameter)
    {
        parameter = new Disposable();
        parameter.Dispose();
    }
}");
    }

    [TestCase("[return: DoNotDispose]")]
    [TestCase("[DoNotDispose]")]
    [TestCase("[KeepsOwnership]")]
    public Task Borrowed_Return_Attribute_Names_Share_Disposal_Enforcement(string attribute)
    {
        return VerifyAsync(@"
public class Test
{
    " + attribute + @" private static Disposable GetShared() => null;
    public void Run() { var resource = GetShared(); {|ERP046:resource|}.Dispose(); }
}");
    }

    [TestCase("await {|ERP046:GetShared()|}.DisposeAsync();")]
    [TestCase("var resource = await GetSharedAsync(); await {|ERP046:resource|}.DisposeAsync();")]
    [TestCase("var resource = await GetSharedAsync().ConfigureAwait(false); await {|ERP046:resource|}.DisposeAsync();")]
    [TestCase("await using var resource = {|ERP046:await GetSharedAsync()|};")]
    [TestCase("var resource = await GetSharedAsync(); await using ({|ERP046:resource|}) { }")]
    [TestCase("var resource = await GetSharedAsync(); await using var alias = {|ERP046:resource|};")]
    public Task DoNotDispose_Also_Prohibits_Async_Disposal(string body)
    {
        return VerifyAsync(@"
public class Resource : System.IAsyncDisposable
{
    public System.Threading.Tasks.ValueTask DisposeAsync() => default;
}
public class Test
{
    [return: DoNotDispose] private static Resource GetShared() => null;
    [return: DoNotDispose] private static System.Threading.Tasks.Task<Resource> GetSharedAsync() => null;
    public async System.Threading.Tasks.Task Run() { " + body + @" }
}");
    }

    [Test]
    public Task DoNotDispose_Returns_Inherit_Interface_Contracts()
    {
        return VerifyAsync(@"
public interface IProvider
{
    [return: DoNotDispose] Disposable GetShared();
}
public class Test : IProvider
{
    public Disposable GetShared() => null;
    public void Run() { var resource = GetShared(); {|ERP046:resource|}.Dispose(); }
}");
    }

    [Test]
    public Task DoNotDispose_Prevents_Transfers_Into_Owning_Returns()
    {
        return VerifyAsync(@"
public class Test
{
    [return: DoNotDispose] private static Disposable GetShared() => null;
    [return: ReturnsOwnership]
    public Disposable Create()
    {
        var resource = GetShared();
        return {|ERP046:resource|};
    }
}");
    }
}

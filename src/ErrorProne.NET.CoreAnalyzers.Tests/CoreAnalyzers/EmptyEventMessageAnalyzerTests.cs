using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.EventSourceAnalysis.EmptyEventMessageAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class EmptyEventMessageAnalyzerTests
    {
        [Test]
        public async Task Warn_On_Indirect_EventSource_Subclass()
        {
            string code = @"
public abstract class BaseEventSource : System.Diagnostics.Tracing.EventSource { }

public sealed class DemoEventSource : BaseEventSource
{
    [System.Diagnostics.Tracing.Event(1, [|Message = """"|])]
    public void AppStarted(string m) => WriteEvent(1, m);
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task No_Warn_On_Null_Message()
        {
            string code = @"
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1, Message = null)]
    public void AppStarted(string m) => WriteEvent(1, m);
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task No_Warn_On_Unrelated_Event_Attribute()
        {
            string code = @"
public class EventAttribute : System.Attribute
{
    public EventAttribute(int id) { }
    public string Message { get; set; }
}

public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [Event(1, Message = """")]
    public void AppStarted(string m) => WriteEvent(1, m);
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_On_Empty_Message()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1, [|Message = """"|])]
    public void AppStarted(string m) => WriteEvent(1, m);
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_On_Empty_Message_With_Other_Named_Args()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1, Level = System.Diagnostics.Tracing.EventLevel.Error, [|Message = """"|])]
    public void AppStarted(string m) => WriteEvent(1, m);
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_On_Empty_Message_From_Const()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    private const string EmptyConst = """";

    [System.Diagnostics.Tracing.Event(1, [|Message = EmptyConst|])]
    public void AppStarted(string m) => WriteEvent(1, m);
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task No_Warn_On_Missing_Message()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1)]
    public void AppStarted(string m) => WriteEvent(1, m);
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task No_Warn_On_Single_Space_Message()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1, Message = "" "")]
    public void AppStarted(string m) => WriteEvent(1, m);
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task No_Warn_On_Non_Empty_Message()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1, Message = ""Started {0}"")]
    public void AppStarted(string m) => WriteEvent(1, m);
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task No_Warn_On_Non_Empty_Const_Message()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    private const string Msg = ""Started"";

    [System.Diagnostics.Tracing.Event(1, Message = Msg)]
    public void AppStarted(string m) => WriteEvent(1, m);
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task No_Warn_On_Empty_Message_In_Non_EventSource_Class()
        {
            // The diagnostic must only fire for types deriving from EventSource.
            string code = @"
public sealed class NotAnEventSource
{
    [System.Diagnostics.Tracing.Event(1, Message = """")]
    public void AppStarted(string m) {}
}";

            await VerifyCS.VerifyAsync(code);
        }
    }
}

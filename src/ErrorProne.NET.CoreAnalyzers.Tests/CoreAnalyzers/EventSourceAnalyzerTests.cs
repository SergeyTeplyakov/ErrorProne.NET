using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.EventSourceAnalysis.EventSourceAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class EventSourceAnalyzerTests
    {
        [Test]
        public async Task Warn_On_Id_Mismatch()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1)]
    public void AppStarted(string message, int favoriteNumber) => [|WriteEvent(2, message, favoriteNumber)|];

    [System.Diagnostics.Tracing.Event(3)]
    public void AppStarted(System.Guid relatedActivityId, string message, int favoriteNumber) => [|WriteEventWithRelatedActivityId(4, relatedActivityId, message, favoriteNumber)|];
}";

            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task Warn_On_Id_Mismatch_Core()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1)]
    public unsafe void AppStarted_With_WriteEventCore()
    {
        // This is not covered by the tool! Fails with IndexOutOfRangeException
        EventData* descrs = stackalloc EventData[0];
        [|WriteEventCore(2, 0, descrs)|];
    }

    [System.Diagnostics.Tracing.Event(3)]
    public unsafe void AppStarted_With_RelatedActivityId_WriteEventCore(System.Guid relatedActivityId)
    {
        // This is not covered by the tool! Fails with IndexOutOfRangeException
        EventData* descrs = stackalloc EventData[0];
        [|WriteEventWithRelatedActivityIdCore(4, &relatedActivityId, 0, descrs)|];
    }
}";

            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task Warn_On_Unsupported_Param_Type()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1)]
    public unsafe void AppStarted_With_WriteEventCore([|System.Text.StringBuilder str|])
    {
        WriteEvent(1, str);
    }

    [System.Diagnostics.Tracing.Event(2)]
    public unsafe void AppStarted_With_WriteEventCore(System.Guid relatedActivityId, [|System.Text.StringBuilder str|])
    {
        WriteEventWithRelatedActivityId(2, relatedActivityId, str);
    }
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_On_Regular_Method()
        {
            string code = @"
using System.Diagnostics.Tracing;
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    public void [|FooBar|]() {}

    public void FooBar2() {[|WriteEvent(55)|];}

    public void FooBar3() {WriteEvent(3);}
    
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_On_Count_Mismatch_Core()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1)]
    public unsafe void AppStarted_With_WriteEventCore(string str)
    {
        // This is not covered by the tool! Fails with IndexOutOfRangeException
        EventData* descrs = stackalloc EventData[0];
        [|WriteEventCore(1, 0, descrs)|];
    }

    [System.Diagnostics.Tracing.Event(2)]
    public unsafe void AppStarted_With_WriteEventCore(System.Guid relatedActivityId, string str)
    {
        // This is not covered by the tool! Fails with IndexOutOfRangeException
        EventData* descrs = stackalloc EventData[0];
        [|WriteEventWithRelatedActivityIdCore(2, &relatedActivityId, 0, descrs)|];
    }
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_On_Unused_Parameter()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1)]
    public void AppStarted(string message, int favoriteNumber) => [|WriteEvent(1, string.Empty, favoriteNumber)|];

    [System.Diagnostics.Tracing.Event(2)]
    public void AppStarted2(System.Guid relatedActivityId, string message, int favoriteNumber) => [|WriteEventWithRelatedActivityId(2, relatedActivityId, string.Empty, favoriteNumber)|];
}";

            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task Warn_On_Params_Mismatch()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1)]
    public void AppStarted(string message, int favoriteNumber) => [|WriteEvent(1, message, favoriteNumber, 42)|];
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_On_Duplicate_Id()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1)]
    public void AppStarted(string message, int favoriteNumber) => WriteEvent(1, message, favoriteNumber);

    [System.Diagnostics.Tracing.Event(1)]
    public void [|AppStarted2|](string message, int favoriteNumber) => WriteEvent(1, message, favoriteNumber);

    const int Id = 1;
    [System.Diagnostics.Tracing.Event(Id)]
    public void [|AppStarted3|](string message, int favoriteNumber) => WriteEvent(1, message, favoriteNumber);

    enum Ids {Event1 = 1}
    [System.Diagnostics.Tracing.Event((int)Ids.Event1)]
    public void [|AppStarted4|](string message, int favoriteNumber) => WriteEvent(1, message, favoriteNumber);
}";

            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task Warn_On_Duplicate_Id_In_Partial_Class()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed partial class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1)]
    public void AppStarted(string message, int favoriteNumber) => WriteEvent(1, message, favoriteNumber);
}

partial class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1)]
    public void [|AppStarted2|](string message, int favoriteNumber) => WriteEvent(1, message, favoriteNumber);
}";

            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task Warn_On_Missing_Write_Call()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed partial class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1)]
    public void AppStarted(string message, int favoriteNumber) => WriteEvent(1, message, favoriteNumber);
}

partial class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(2)]
    public void [|AppStarted2|](string message, int favoriteNumber) => FooBar();

    [System.Diagnostics.Tracing.Event(3)]
    public void [|AppStarted3|](System.Guid relatedActivityId, string message, int favoriteNumber) => FooBar();

    private void [|FooBar|]() {}
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_On_Arg_Mismatch()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1)]
    public void AppStarted(string message, int favoriteNumber) => [|WriteEvent(2, message, favoriteNumber)|];
}";

            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task Warn_On_Incorrect_Usage_Of_RelativeActivityId()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1)]
    public void [|Event1|](System.Guid guid) => WriteEventWithRelatedActivityId(1, guid);

    [System.Diagnostics.Tracing.Event(2)]
    public void [|Event2|](int x) => WriteEventWithRelatedActivityId(2, System.Guid.NewGuid(), x);

[System.Diagnostics.Tracing.Event(3)]
    public void [|Event3|](int x, System.Guid relatedActivityId) => WriteEventWithRelatedActivityId(3, relatedActivityId, x);
}";

            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task Warn_On_Id_Mismatch_Const()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    private const int AppStartedId = 2;
    [System.Diagnostics.Tracing.Event(1)]
    public void AppStarted(string message, int favoriteNumber) => [|WriteEvent(AppStartedId, message, favoriteNumber)|];
}";

            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task No_Warn_On_Correct_Id()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1)]
    public void Event1(string message, int favoriteNumber) => WriteEvent(1, message, favoriteNumber);

    const int Id2 = 2;
    [System.Diagnostics.Tracing.Event(2)]
    public void Event2(string message, int favoriteNumber) => WriteEvent(Id2, message, favoriteNumber);

    const int Id3 = 3;
    [System.Diagnostics.Tracing.Event(Id3)]
    public void Event3(string message, int favoriteNumber) => WriteEvent((int)Ids.Event3, message, favoriteNumber);

    const int Id4 = 4;
    [System.Diagnostics.Tracing.Event((int)Ids.Event4)]
    public void Event4(string message, int favoriteNumber) => WriteEvent((int)Ids.Event4, message, favoriteNumber);

    private enum Ids
    {
        Event3 = 3,
        Event4 = 4
    }
}";

            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task No_Warn_On_Correct_Id_And_Params()
        {
            string code = @"
using System.Diagnostics.Tracing;
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    public DemoEventSource() {}
    ~DemoEventSource() {}
    public int X {get;set;}
    public event System.EventHandler MyEvent;
    [Event(1)]
    public void MyEvent1(string str)
    {
        WriteEvent(1, str);
    }
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task No_Warn_On_Non_EventSource()
        {
            string code = @"
using System.Diagnostics.Tracing;
public sealed class DemoEventSource
{
    public void MyEvent1(string str)
    {
    }
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task No_Warn_On_Static_Method()
        {
            string code = @"
using System.Diagnostics.Tracing;
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [Event(5)]
    public void MyEvent1(string str)
    {
        WriteEvent(5, str);
    }

    public int FooBar() => 42;

    public static void FooBar2() {}
}";

            await VerifyCS.VerifyAsync(code);
        }
        
    }
}
using ErrorProne.Samples.StructAnalyzers;
using System.Diagnostics.Tracing;
using System;

namespace ErrorProne.Samples.CoreAnalyzers;

public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    private enum Ids
    {
        Id3 = 3
    }

    public static readonly DemoEventSource Log = new DemoEventSource();
    private const int x = 42;

    private const int Id = 3;

    [Event(Id)]
    public void MyEvent1(string str, int n)
    {
        WriteEvent(Id, str);
    }
    
    [Event(3)]
    public void MyEvent5(string str)
    {
        WriteEvent(5, str, 42);
    }

}
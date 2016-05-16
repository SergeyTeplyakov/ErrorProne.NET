using System;
using System.Diagnostics;

namespace ErrorProne.Samples.DebuggerDisplay
{
    // DebuggerDisplay attribute requiremenents:
    // instance/static Field/property/method with any visibility
    // Format: nq - no quotes. What else?
    // Method could have arguments! If method has default arguments, everything is fine!
    // If argument types are incompatible, then error will happen in runtime!
[DebuggerDisplay("X: {this.foo(42),nq}")]
public class CustomType
{
    private string _something = "foo";

    private int foo(dynamic n)
    {
        return (int)n;
    }
}

    public class RunDebuggerDisplaySample
    {
        public static void Run()
        {
            var ct = new[]
            {
                new CustomType(), 
            };
            Console.WriteLine("done");
        }
    }
}
using System;
using System.Diagnostics;

namespace ErrorProne.Samples.DebuggerDisplay
{
    // DebuggerDisplay attribute requiremenents:
    // instance/static Field/property/method with any visibility
    // Format: nq - no quotes. What else?
    // Method could have arguments! If method has default arguments, everything is fine!
    // If argument types are incompatible, then error will happen in runtime!
    [DebuggerDisplay("X: {ToDisplayString()}")]
    //               ~~~~~~~~~~~~~~~~~~~~~~~~
    // Expression 'ToStrinString(string.Emmpty)' is invalid: 
    //    'The name '_internalStringRepr' does not exists in current context'
    public class CustomType
    {
        private string _internalStringRepresentation = "foo";

        private void ToDisplayString()
        {
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
            var x = $"{Run}";
            Console.WriteLine("done");
        }
    }
}
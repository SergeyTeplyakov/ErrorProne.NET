using System;
using System.Diagnostics;

namespace ErrorProne.Samples.DebuggerDisplay
{
    [DebuggerDisplay("{ToDisplayString(42)}")]
    //               ~~~~~~~~~~~~~~~~~~~~~~~~
    // Expression 'ToStrinString(string.Emmpty)' is invalid: 
    //    'Argument 1: cannot convert from 'int' to 'string''
    // 
    [DebuggerDisplay("{_content}")]
    //               ~~~~~~~~~~~~
    // Expression '_content' is invalid:
    //   'The name '_content' does not exists in current context'
    public class CustomType
    {
        private string _innerCcontent = "empty";
        private string ToDisplayString(string x)
        {
            return string.Empty;
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
            //var x = $"{Run}";
            Console.WriteLine("done");
        }
    }
}
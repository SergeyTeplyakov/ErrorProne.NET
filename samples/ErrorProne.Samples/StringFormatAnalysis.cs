using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JetBrains.Annotations
{
    public class StringFormatMethodAttribute : Attribute
    {
        public StringFormatMethodAttribute(string name) { }
    }
}

namespace ErrorProne.Samples
{
    public class StringFormatAnalysis
    {
        [JetBrains.Annotations.StringFormatMethod("message")]
        public static void WriteLog(string message, params object[] args) { }

        public void Sample()
        {
            // Format argument was not provided

            // Argument 3 was not provided
            Console.WriteLine("{0}, {3}", 1);
            // Argument 2 was not provided
            var s = string.Format(format: "{2}", arg0: 1);
            // Argument 1 was not provided
            WriteLog("{1}", 1);

            // Excessive arguments

            // Argument 3 was not used in the format string
            // Rule is working for const fields variables and
            // with static readonly fields/properties
            const string format = "{0}, {1}";
            Console.WriteLine(format, 1, 2, 3);



            // Format argument is a valid format string
            s = string.Format("{1\\d(");



            // Regex pattern is invalid: parsing "\d(" - Not enough )'s.
            var regex = new Regex("\\d(");
        }
    }
}

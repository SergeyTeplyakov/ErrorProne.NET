using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ErrorProne.Samples
{
    class Playground
    {
        public void Play()
        {
            Enumerable.Range(1, 10);

            string s = "foo";
            s.Substring(42);

            Enumerable.Range(1, 10).ToImmutableList();

            s = string.Format("{0}\{", 1, 3);
            var regex = new Regex("\\d(");
        }
    }
}

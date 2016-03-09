using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ErrorProne.Samples.ExceptionHandling
{
    public class AsyncMethods
    {
        public async Task FooAsync(string s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));

            await Task.Delay(42);
        }

        public IEnumerable<string> Iterator(string s)
        {
            if (s == null) throw new ArgumentException();

            yield return "";
        }
    }
}
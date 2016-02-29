using System;
using System.Linq;

namespace ErrorProne.Samples.WorkInProgress
{
    class Foo
    {
        private readonly int _s;// = "f";
        private readonly int n;

        public void Method()
        {
            Console.WriteLine(_s);
            Enumerable.Range(1, 10);
        }
    }

    public class WorkInProgress
    {
         
    }
}
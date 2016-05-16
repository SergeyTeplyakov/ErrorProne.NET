using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorProne.Samples
{
    class Foo2
    {
        public readonly Immutable m;
        private readonly int n;
        public static void Test()
        {
            var foo = new Foo();
            foo.m.PrintToConsole();
            //var r = foo.n.CompareTo(42);
        }
    }
    class Program
    {
        static void Main(string[] args)
        {
            DebuggerDisplay.RunDebuggerDisplaySample.Run();
        }
    }
}

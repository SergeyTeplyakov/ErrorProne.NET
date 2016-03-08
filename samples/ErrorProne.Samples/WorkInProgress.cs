using System;
using System.Linq;

namespace ErrorProne.Samples.WorkInProgress
{
    struct CustomStruct { }
    class Foo2
    {
        public readonly CustomStruct _m;
    }

    struct Custom { }
    class Base
    {
        private readonly Custom field = new Custom();
        //protected int field;
        // running the build!!
    }

    class Derived2 : Base
    {
        public Derived2()
        {
            //field = 42;
        }
    }








    class Derived : Base
{
    //public override int S => 42;
}




    class Foo
{
    public int S { get; }
}

    public class WorkInProgress
    {
         
    }
}
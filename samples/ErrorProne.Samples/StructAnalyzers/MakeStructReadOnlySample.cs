using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorProne.Samples.StructAnalyzers
{
    // Struct 'MyStruct' can be made readonly
    public struct MyStruct
    {
        private readonly long _x;
        private readonly long _y;
        public void FooBar() { }
        // Struct 'MyStruct' with the default Equals and HashCode implementation
        // is used as a key in a hash table.
        private static Dictionary<MyStruct, string> Table;
    }

    class MakeStructReadOnlySample
    {
        // Non-readonly struct 'MyStruct' is passed as in-parameter 'ms'
        public static void InParameter(in MyStruct ms)
        {
            // 
            ms.ToString();
        }

        // Non-readonly struct 'MyStruct' returned by readonly reference
        public static ref readonly MyStruct Return() => throw null;

        public static void RefReadonly()
        {
            var s = new MyStruct();
            // Non-readonly struct 'MyStruct' is used as ref-readonly local 'ms'
            ref readonly MyStruct ms = ref s;
        }

        // The analysis is triggered only for "large" readonly structs:
        // Use in-modifier for passing a read-only struct 'MyReadonlyStruct'
        public static void PassByValue(MyReadonlyStruct ms) { }

        public static void HiddenCopy(in MyStruct ms)
        {
            // Some analyzers are triggered only for "large" structs to avoid extra noise
            // Hidden copy copy
            
            // Expression 'FooBar' causes a hidden copy of a non-readonly struct 'MyStruct'
            ms.FooBar();
            // ~~~~~~

            ref readonly MyStruct local = ref ms;

            // Hidden copy as well
            local.FooBar();
            //    ~~~~~~

            // Hidden copy as well
            _staticStruct.FooBar();
            //            ~~~~~~
        }

        private static readonly MyStruct _staticStruct;
    }

    public readonly struct MyReadonlyStruct { private readonly long l1, l2; }
}

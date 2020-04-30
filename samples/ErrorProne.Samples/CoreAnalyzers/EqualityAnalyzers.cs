using System;

namespace ErrorProne.Samples.CoreAnalyzers
{
    public class EqualityAnalyzers
    {
        public class FooBar : IEquatable<FooBar>
        {
            private string _s;

            // Suspicious equality implementation: parameter 'other' is never used
            public bool Equals(FooBar other)
            //                                 ~~~~~
            {
                return _s == "42";
            }
        }

        public class Baz : IEquatable<Baz>
        {
            private string _s;

            // Suspicious equality implementation: no instance members are used
            public bool Equals(Baz other)
            //          ~~~~~~
            {
                return other != null;
            }
        }
    }
}
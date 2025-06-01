using System;

namespace ErrorProne.NET.Utils
{
    [Flags]
    public enum ValueTypeEqualityImplementations
    {
        None = 0,
        Equals = 1 << 0,
        GetHashCode = 1 << 1,
        All = Equals | GetHashCode,
    }
}
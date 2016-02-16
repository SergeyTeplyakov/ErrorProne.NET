using System;

namespace ErrorProne.NET.Utils
{
    public static class LazyEx
    {
        public static Lazy<T> Create<T>(this Func<T> func)
        {
            return new Lazy<T>(func);
        }
    }
}
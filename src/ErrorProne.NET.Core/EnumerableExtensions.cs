using System;
using System.Collections.Generic;
using System.Linq;

namespace ErrorProne.NET.Core
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Allocation-free implementation of <see cref="Enumerable.Any{TSource}(System.Collections.Generic.IEnumerable{TSource})"/>.
        /// </summary>
        public static bool Any<TSource>(this List<TSource> source, Func<TSource, bool> predicate)
        {
            foreach (TSource element in source)
            {
                if (predicate(element))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
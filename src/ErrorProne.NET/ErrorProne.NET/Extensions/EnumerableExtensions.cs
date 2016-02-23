using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace ErrorProne.NET.Extensions
{
    internal static class EnumerableExtensions
    {
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> sequence)
        {
            return new HashSet<T>(sequence);
        }  
        
        public static Dictionary<TKey, TValue> ToDictionarySafe<T, TKey, TValue>(this IEnumerable<T> sequence,
            Func<T, TKey> keySelector, Func<T, TValue> valueSelector)
        {
            Contract.Requires(sequence != null);
            Contract.Requires(keySelector != null);
            Contract.Requires(valueSelector != null);

            var result = new Dictionary<TKey, TValue>();

            foreach (var e in sequence)
            {
                var key = keySelector(e);
                var value = valueSelector(e);
                if (!result.ContainsKey(key))
                {
                    result.Add(key, value);
                }
            }

            return result;
        }
    }
}
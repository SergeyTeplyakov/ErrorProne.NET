using System;
using System.Collections.Generic;

namespace ErrorProne.NET.Core
{
    public static class EnumerableExtensions
    {
        public static T MinByOrDefault<T, TKey>(this IEnumerable<T> items, Func<T, TKey> keySelector, IComparer<TKey> keyComparer = null)
        {
            keyComparer = keyComparer ?? Comparer<TKey>.Default;
            T maxItem = default;
            TKey maxKey = default;
            bool isFirst = true;

            foreach (var item in items)
            {
                var currentKey = keySelector(item);
                if (isFirst || keyComparer.Compare(currentKey, maxKey) < 0)
                {
                    isFirst = false;
                    maxItem = item;
                    maxKey = currentKey;
                }
            }

            return maxItem;
        }

        public static Dictionary<TKey, List<TValue>> GroupToDictionary<TKey, TValue>(this IEnumerable<(TKey, TValue)> sequence)
        {
            Dictionary<TKey, List<TValue>> result = new Dictionary<TKey, List<TValue>>();

            foreach (var (key, value) in sequence)
            {
                if (result.TryGetValue(key, out var list))
                {
                    list.Add(value);
                }
                else
                {
                    result.Add(key, new List<TValue>() {value});
                }
            }

            return result;
        }
    }
}

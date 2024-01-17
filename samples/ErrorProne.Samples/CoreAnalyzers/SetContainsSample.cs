using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections;

namespace ErrorProne.Samples.CoreAnalyzers;

public class SetContainsSample
{
    public class MySet<T> : ISet<T>
    {
        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <inheritdoc />
        void ICollection<T>.Add(T item)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void ExceptWith(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void IntersectWith(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool IsSubsetOf(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool IsSupersetOf(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool Overlaps(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool SetEquals(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void UnionWith(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        bool ISet<T>.Add(T item)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Clear()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool Contains(T item)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool Remove(T item)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public int Count { get; }

        /// <inheritdoc />
        public bool IsReadOnly { get; }
    }

    public void ShowWarnings()
    {
        var set = new HashSet<string>();
        // Ok. O(1) search
        var r = set.Contains("42");
        // Ok as well, this overload checks for `ICollection<T>` and calls
        // Contains(T) to get O(1) search.
        r = Enumerable.Contains(set, "2");

        // Not OK: O(N) search!
        r = set.Contains("12", StringComparer.Ordinal);

        var setOfInts = new HashSet<int>();
        r = setOfInts.Contains(42, EqualityComparer<int>.Default);
    }
}
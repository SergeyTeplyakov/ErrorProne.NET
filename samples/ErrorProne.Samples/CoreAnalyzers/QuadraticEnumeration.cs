using System.Collections.Generic;
using System.Linq;

namespace ErrorProne.Samples.CoreAnalyzers
{
    public class QuadraticEnumeration
    {
        // Q1 — LINQ-enumerating call whose source is the same enumerable being iterated. O(N^2).
        public int SameSourceCount(IEnumerable<int> coll)
        {
            var total = 0;
            foreach (var x in coll)
            {
                var n = coll.Count(); // ❌ EPC39 (Q1)
                total += x * n;
            }

            return total;
        }

        // Q2 — re-enumeration of a deferred IEnumerable<T> inside a loop. Each iteration re-runs Where().
        public int DeferredInLoop(IEnumerable<int> source, int[] outer)
        {
            IEnumerable<int> q = source.Where(x => x > 0);
            var total = 0;
            foreach (var i in outer)
            {
                total += q.Count(); // ❌ EPC39 (Q2)
            }

            return total;
        }

        // Q4 — nested foreach over the same source.
        public int NestedForeachSameSource(List<int> coll)
        {
            var pairs = 0;
            foreach (var x in coll)
            {
                foreach (var y in coll) // ❌ EPC39 (Q4)
                {
                    if (x + y == 0) pairs++;
                }
            }

            return pairs;
        }

        // ✅ Fixes:

        public int DeferredInLoopFixed(IEnumerable<int> source, int[] outer)
        {
            var q = source.Where(x => x > 0).ToList(); // materialize once
            var total = 0;
            foreach (var i in outer)
            {
                total += q.Count;
            }

            return total;
        }
    }
}

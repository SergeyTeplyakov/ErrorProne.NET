using System.Collections.Generic;
using System.Linq;

namespace ErrorProne.Samples.CoreAnalyzers
{
    public class PrivateMultipleEnumeration
    {
        // ❌ Helper enumerates `items` twice. Any private caller passing a deferred query is reported.
        private int Helper(IEnumerable<int> items)
        {
            var c = items.Count();
            return c + items.Sum();
        }

        // ✅ Materialized version — safe to enumerate multiple times.
        private int HelperFixed(IReadOnlyList<int> items)
        {
            var c = items.Count;
            var sum = 0;
            foreach (var x in items) sum += x;
            return c + sum;
        }

        static IEnumerable<int> Produce()
        {
            yield return 1;
            yield return 2;
            yield return 3;
        }

        public void Caller_Where(IEnumerable<int> source)
        {
            Helper(source.Where(x => x > 0)); // ❌ EPC40 — .Where(...) is deferred
        }

        public void Caller_Iterator()
        {
            Helper(Produce()); // ❌ EPC40 — iterator method
        }

        public void Caller_Range()
        {
            Helper(Enumerable.Range(0, 100)); // ❌ EPC40 — Enumerable.Range
        }

        public void Caller_LocalQuery(IEnumerable<int> source)
        {
            var query = source.Where(x => x > 0);
            Helper(query); // ❌ EPC40 — 1-hop provenance back to .Where(...)
        }

        public void Caller_TwoHopQuery(IEnumerable<int> source)
        {
            var step1 = source.Where(x => x > 0);
            var step2 = step1;
            Helper(step2); // ❌ EPC40 — 2-hop provenance back to .Where(...)
        }

        // ✅ Materialized argument — no warning.
        public void Caller_List(List<int> items)
        {
            Helper(items);
        }

        // ✅ Materialize the query at the call site.
        public void Caller_FixedByMaterialization(IEnumerable<int> source)
        {
            Helper(source.Where(x => x > 0).ToList());
        }
    }
}

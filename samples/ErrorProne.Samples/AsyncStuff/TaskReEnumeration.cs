using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ErrorProne.Samples.AsyncStuff
{
    public class TaskReEnumeration
    {
        // Canonical bug: WhenAll consumes the deferred IEnumerable<Task<TResult>>; the subsequent foreach
        // re-runs Select() and starts a NEW set of tasks that are completely unrelated to the ones we awaited.
        public async Task<int> ComputeTotalAsync(IEnumerable<int> ids)
        {
            var tasks = ids.Select(async id =>
            {
                await Task.Delay(1);
                return id * 2;
            });

            await Task.WhenAll(tasks);

            var total = 0;
            foreach (var t in tasks) // ❌ EPC38 — second enumeration starts new tasks
            {
                total += await t;
            }

            return total;
        }

        // Two LINQ aggregations on the same deferred enumerable: also a re-enumeration bug.
        public int CountAndSum(IEnumerable<Task<int>> tasks)
        {
            var count = tasks.Count();
            var sum = tasks.Sum(t => t.Result); // ❌ EPC38
            return count + sum;
        }

        // Fix: materialize the deferred enumerable first so re-iteration is safe.
        public async Task<int> ComputeTotalFixedAsync(IEnumerable<int> ids)
        {
            var tasks = ids.Select(async id =>
            {
                await Task.Delay(1);
                return id * 2;
            }).ToArray();

            await Task.WhenAll(tasks);

            var total = 0;
            foreach (var t in tasks) // ✅ safe — `tasks` is Task<int>[]
            {
                total += await t;
            }

            return total;
        }
    }
}

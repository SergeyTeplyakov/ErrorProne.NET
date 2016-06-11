using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ErrorProne.Samples.AsyncStuff
{
    public class RedundantAwait
    {
        public async Task<int> FooAsync(string s)
        {
            ConfiguredTaskAwaitable<int> tsk = Task.FromResult(1).ConfigureAwait(false);
            var x = tsk.GetAwaiter().GetResult();
            if (s == null) return await Task.FromResult(1).ConfigureAwait(false);

            return await Task.FromResult(42);
        }

        private static async Task<int> Foo(string arg)
        {
            return await Task.FromResult(42) + 1;
        }
    }
}
using System.Threading.Tasks;

namespace ErrorProne.Samples.AsyncStuff
{
    public class RedundantAwait
    {
        public async Task<int> FooAsync(string s)
        {
            if (s == null) return await Task.FromResult(1);

            return await Task.FromResult(42);
        }
    }
}
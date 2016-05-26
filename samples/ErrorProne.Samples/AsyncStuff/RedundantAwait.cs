using System.Threading.Tasks;

namespace ErrorProne.Samples.AsyncStuff
{
    public class RedundantAwait
    {
        public async Task<int> FooAsync()
        {
            return await Task.FromResult(42);
        }
    }
}
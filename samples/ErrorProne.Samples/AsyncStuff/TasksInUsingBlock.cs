using System.Threading;
using System.Threading.Tasks;

namespace ErrorProne.Samples.AsyncStuff;

public static class TasksInUsingBlock
{
    public static async Task UsingSemaphore(SemaphoreSlim semaphore)
    {
        using var _ = semaphore.UseSemaphoreAsync();
        // Assuming we're running the code exclusively!
        // But do we?
        await Task.Yield();
    }

    public static async Task<SemaphoreReleaser> UseSemaphoreAsync(this SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        return new SemaphoreReleaser(semaphore);
    }

    public record struct SemaphoreReleaser(SemaphoreSlim Semaphore)
    {
        public void Dispose() => Semaphore.Release();
    }

}
# EPC36 - Do not use async delegates with Task.Factory.StartNew and TaskCreationOptions.LongRunning

This analyzer warns when an async delegate is used with `Task.Factory.StartNew` and `TaskCreationOptions.LongRunning`.

## Description

`TaskCreationOptions.LongRunning` is intended for long-running synchronous work that would otherwise block a thread pool thread for an extended period. When used with async delegates, it defeats the purpose of async programming by:

1. **Wasting thread pool threads**: The LongRunning option creates a dedicated thread that will be used only before the first `await` in the async delegate (or async method).
3. **Creating confusion**: The intent of the code becomes unclear - is it meant to be long-running synchronous work or efficient async work? Is it really the case that the synchronous block of the async method is taking a long time, or it's just a mistake?

## Code that triggers the analyzer

❌ **Bad** - Using async lambda with LongRunning:
```csharp
using System;
using System.Threading.Tasks;

class Example
{
    void BadExamples()
    {
        // ❌ EPC36: Async lambda with LongRunning
        Task.Factory.StartNew(async () => 
        {
            await SomeAsyncOperation();
        }, TaskCreationOptions.LongRunning);

        // ❌ EPC36: Async delegate with LongRunning
        Task.Factory.StartNew(async delegate()
        {
            await SomeAsyncOperation();
        }, TaskCreationOptions.LongRunning);

        // ❌ EPC36: Async method reference with LongRunning
        Task.Factory.StartNew(SomeAsyncMethod, TaskCreationOptions.LongRunning);

        // ❌ EPC36: Combined options including LongRunning
        Task.Factory.StartNew(async () => 
        {
            await SomeAsyncOperation();
        }, TaskCreationOptions.LongRunning | TaskCreationOptions.AttachedToParent);
    }

    async Task SomeAsyncMethod() => await Task.Delay(100);
    async Task SomeAsyncOperation() => await Task.Delay(1000);
}
```

## How to fix

Choose the appropriate approach based on your intent:

✅ **Good** - Use `Task.Run` for async delegates:
```csharp
using System;
using System.Threading.Tasks;

class Example
{
    void GoodExamples()
    {
        // ✅ Correct: Use Task.Run for async work
        Task.Run(async () => 
        {
            await SomeAsyncOperation();
        });

        // ✅ Correct: Task.Run with async method reference
        Task.Run(SomeAsyncMethod);

        // ✅ Correct: If you need specific task creation options (except LongRunning)
        Task.Factory.StartNew(async () => 
        {
            await SomeAsyncOperation();
        }, TaskCreationOptions.AttachedToParent);
    }

    async Task SomeAsyncMethod() => await Task.Delay(100);
    async Task SomeAsyncOperation() => await Task.Delay(1000);
}
```

✅ **Good** - Use LongRunning only for truly long-running synchronous work:
```csharp
using System;
using System.Threading.Tasks;

class Example
{
    void LongRunningSyncWork()
    {
        // ✅ Correct: LongRunning with synchronous work
        Task.Factory.StartNew(() => 
        {
            // Long-running CPU-intensive or blocking synchronous operation
            for (int i = 0; i < 1_000_000_000; i++)
            {
                // Some heavy computation
                ProcessData(i);
            }
        }, TaskCreationOptions.LongRunning);

        // ✅ Correct: LongRunning with blocking I/O that can't be made async
        Task.Factory.StartNew(() => 
        {
            // Legacy blocking operation that can't be easily made async
            LegacyBlockingOperation();
        }, TaskCreationOptions.LongRunning);
    }

    void ProcessData(int value) { /* CPU work */ }
    void LegacyBlockingOperation() { /* Blocking I/O */ }
}
```

## When to use each approach

- **`Task.Run`**: For async operations or when you need to run async code on the thread pool
- **`Task.Factory.StartNew` without LongRunning**: When you need specific task creation options but still want efficient async execution
- **`Task.Factory.StartNew` with LongRunning**: Only for long-running synchronous operations that would otherwise tie up thread pool threads

## Performance impact

Using async delegates with LongRunning can lead to:
- **Thread pool starvation**: Each LongRunning task consumes a dedicated thread
- **Increased memory usage**: Each thread consumes approximately 1MB of virtual memory for its stack
- **Reduced scalability**: The application cannot efficiently handle many concurrent operations

## Examples in context

❌ **Bad** - Common anti-pattern:
```csharp
// This creates a dedicated thread that just waits for async operations
var tasks = new List<Task>();
for (int i = 0; i < 100; i++)
{
    tasks.Add(Task.Factory.StartNew(async () => 
    {
        await DownloadFileAsync($"file{i}.txt"); // ❌ EPC36
    }, TaskCreationOptions.LongRunning));
}
await Task.WhenAll(tasks);
```

✅ **Good** - Efficient async pattern:
```csharp
// This uses the thread pool efficiently for async operations
var tasks = new List<Task>();
for (int i = 0; i < 100; i++)
{
    tasks.Add(Task.Run(async () => 
    {
        await DownloadFileAsync($"file{i}.txt"); // ✅ Correct
    }));
}
await Task.WhenAll(tasks);
```

## See also

- [Task.Run vs Task.Factory.StartNew](https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/task-run-vs-task-factory-startnew)
- [TaskCreationOptions.LongRunning documentation](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskcreationoptions)
- [Async/await best practices](https://docs.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)

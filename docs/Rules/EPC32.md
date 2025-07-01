# EPC32 - TaskCompletionSource should use RunContinuationsAsynchronously

This analyzer detects when `TaskCompletionSource` is created without the `TaskCreationOptions.RunContinuationsAsynchronously` flag, which can lead to deadlocks.

## Description

The analyzer warns when `TaskCompletionSource` instances are created without using `TaskCreationOptions.RunContinuationsAsynchronously`. Without this flag, continuations run synchronously on the thread that completes the task, which can cause deadlocks and unexpected blocking behavior.

## Code that triggers the analyzer

```csharp
public class Example
{
    // Bad: TaskCompletionSource without RunContinuationsAsynchronously
    private TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();
    
    public Task<string> GetResultAsync()
    {
        return tcs.Task;
    }
    
    public void CompleteTask(string result)
    {
        // Continuations will run synchronously on this thread
        tcs.SetResult(result); // Potential deadlock risk
    }
}
```

```csharp
public class Worker
{
    // Also problematic: creating without the flag
    public Task<int> ProcessAsync()
    {
        var completionSource = new TaskCompletionSource<int>();
        
        ThreadPool.QueueUserWorkItem(_ =>
        {
            var result = DoWork();
            completionSource.SetResult(result); // Continuations block this thread
        });
        
        return completionSource.Task;
    }
}
```

## How to fix

Always use `TaskCreationOptions.RunContinuationsAsynchronously`:

```csharp
public class Example
{
    // Good: TaskCompletionSource with RunContinuationsAsynchronously
    private TaskCompletionSource<string> tcs = new TaskCompletionSource<string>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    
    public Task<string> GetResultAsync()
    {
        return tcs.Task;
    }
    
    public void CompleteTask(string result)
    {
        // Continuations will run asynchronously
        tcs.SetResult(result); // No deadlock risk
    }
}
```

```csharp
public class Worker
{
    // Good: creating with the flag
    public Task<int> ProcessAsync()
    {
        var completionSource = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        
        ThreadPool.QueueUserWorkItem(_ =>
        {
            var result = DoWork();
            completionSource.SetResult(result); // Safe
        });
        
        return completionSource.Task;
    }
}
```

For generic TaskCompletionSource:

```csharp
public class GenericExample<T>
{
    // Good: with RunContinuationsAsynchronously
    private TaskCompletionSource<T> CreateCompletionSource()
    {
        return new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    
    // Alternative: create a helper method
    private static TaskCompletionSource<TResult> CreateTcs<TResult>()
    {
        return new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
```

## Why this flag is important

Without `RunContinuationsAsynchronously`:
- Continuations run synchronously on the completing thread
- Can block the completing thread unexpectedly
- May cause deadlocks in certain scenarios
- Can lead to poor performance in high-concurrency situations

With `RunContinuationsAsynchronously`:
- Continuations are queued to run asynchronously
- Completing thread is not blocked
- Reduces deadlock risk
- Better scalability and performance

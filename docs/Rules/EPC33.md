# EPC33 - Do not use Thread.Sleep in async methods

This analyzer detects the use of `Thread.Sleep` in async methods, which blocks the thread and defeats the purpose of async programming.

## Description

The analyzer warns when `Thread.Sleep` is used in async methods. This blocks the current thread, which is contrary to the non-blocking nature of async/await patterns. In async methods, you should use `Task.Delay` with await instead.

## Code that triggers the analyzer

```csharp
public async Task ProcessAsync()
{
    // Bad: Thread.Sleep blocks the thread
    Thread.Sleep(1000); // Blocks for 1 second
    
    await DoWorkAsync();
    
    // Also bad: Thread.Sleep with TimeSpan
    Thread.Sleep(TimeSpan.FromSeconds(2));
}

public async void HandleEventAsync()
{
    // Bad: blocking in async event handler
    Thread.Sleep(500);
    await UpdateUIAsync();
}

public async Task<string> GetDataAsync()
{
    // Bad: mixed blocking and async code
    Thread.Sleep(100); // Simulate delay - wrong way
    return await FetchDataAsync();
}
```

## How to fix

Use `Task.Delay` with await instead:

```csharp
public async Task ProcessAsync()
{
    // Good: Task.Delay doesn't block the thread
    await Task.Delay(1000); // Non-blocking delay
    
    await DoWorkAsync();
    
    // Good: Task.Delay with TimeSpan
    await Task.Delay(TimeSpan.FromSeconds(2));
}

public async void HandleEventAsync()
{
    // Good: non-blocking delay in async event handler
    await Task.Delay(500);
    await UpdateUIAsync();
}

public async Task<string> GetDataAsync()
{
    // Good: all async operations
    await Task.Delay(100); // Non-blocking simulation
    return await FetchDataAsync();
}
```

For cancellation support:

```csharp
public async Task ProcessAsync(CancellationToken cancellationToken = default)
{
    // Good: Task.Delay with cancellation support
    await Task.Delay(1000, cancellationToken);
    
    await DoWorkAsync(cancellationToken);
}

public async Task LongRunningProcessAsync(CancellationToken cancellationToken)
{
    for (int i = 0; i < 100; i++)
    {
        await ProcessItemAsync(i);
        
        // Good: cancellable delay between iterations
        await Task.Delay(100, cancellationToken);
    }
}
```

## Why Thread.Sleep is problematic in async methods

- Blocks the current thread, preventing it from handling other work
- Defeats the scalability benefits of async/await
- Can cause thread pool starvation
- Doesn't respect cancellation tokens
- May cause deadlocks in certain contexts (like ASP.NET)

## Benefits of Task.Delay

- Non-blocking - thread can handle other work
- Supports cancellation via CancellationToken
- Integrates properly with async/await patterns
- Better resource utilization
- Proper behavior in async contexts

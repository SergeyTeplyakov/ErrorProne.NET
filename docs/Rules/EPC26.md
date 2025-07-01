# EPC26 - Do not use tasks in using block

This analyzer detects when `Task` objects are used in `using` statements, which is problematic because tasks should not be explicitly disposed.

## Description

The analyzer warns when Task objects are used in `using` statements or `using` declarations. While Task implements `IDisposable`, calling `Dispose()` on a Task is almost never the right thing to do and can cause issues.

## Code that triggers the analyzer

```csharp
public async Task Example()
{
    // Bad: using statement with Task
    using (var task = SomeAsyncMethod())
    {
        await task;
    } // Task.Dispose() called here - problematic!
    
    // Also bad: using declaration
    using var task2 = GetTaskAsync();
    await task2; // task2.Dispose() called at end of scope
}

public async Task AnotherExample()
{
    // Bad: Task in using block
    using var delayTask = Task.Delay(1000);
    await delayTask;
}
```

## How to fix

Simply remove the `using` statement and use the task directly:

```csharp
public async Task Example()
{
    // Good: just await the task directly
    await SomeAsyncMethod();
    
    // Or store in variable if needed
    var task = GetTaskAsync();
    await task;
    // No explicit disposal needed
}

public async Task AnotherExample()
{
    // Good: use Task.Delay directly
    await Task.Delay(1000);
}
```

If you need to handle task completion or cancellation:

```csharp
public async Task ExampleWithCancellation()
{
    using var cts = new CancellationTokenSource();
    
    // Good: dispose the CancellationTokenSource, not the Task
    var task = SomeAsyncMethod(cts.Token);
    
    try
    {
        await task;
    }
    catch (OperationCanceledException)
    {
        // Handle cancellation
    }
    // CancellationTokenSource gets disposed, not the Task
}
```

## Why tasks shouldn't be disposed

- Task objects don't own managed resources and should not be explicitly disposed

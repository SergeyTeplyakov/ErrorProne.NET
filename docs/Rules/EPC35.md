# EPC35 - Do not block unnecessarily in async methods

This analyzer detects synchronous blocking operations inside async methods that can lead to deadlocks and poor performance.

## Description

The analyzer warns when you use blocking operations like `.Wait()`, `.Result`, or `.GetAwaiter().GetResult()` on Task-like types inside async methods. These blocking operations defeat the purpose of async programming and can cause deadlocks, especially in UI applications or when there's a limited synchronization context.

## Code that triggers the analyzer

```csharp
public async Task ProcessAsync()
{
    // Using .Wait() blocks the thread
    SomeAsyncMethod().Wait(); // ❌ EPC35
    
    // Using .Result property blocks the thread
    var result = SomeAsyncMethod().Result; // ❌ EPC35
    
    // Using GetAwaiter().GetResult() blocks the thread
    var data = GetDataAsync().GetAwaiter().GetResult(); // ❌ EPC35
    
    // More async work
    await AnotherAsyncMethod();
}
```

## How to fix

Use `await` instead of blocking operations:

```csharp
public async Task ProcessAsync()
{
    // Use await instead of .Wait()
    await SomeAsyncMethod(); // ✅ Correct
    
    // Use await instead of .Result
    var result = await SomeAsyncMethod(); // ✅ Correct
    
    // Use await instead of GetAwaiter().GetResult()
    var data = await GetDataAsync(); // ✅ Correct
    
    // More async work
    await AnotherAsyncMethod();
}
```

## Why this is important

1. **Deadlock Prevention**: Blocking on async operations can cause deadlocks, especially when there's a synchronization context (like in UI applications)
2. **Performance**: Blocking ties up thread pool threads unnecessarily
3. **Scalability**: Proper async/await allows better resource utilization
4. **Consistency**: Mixing blocking and async patterns makes code harder to reason about

## Examples of problematic patterns

```csharp
// UI event handler - high risk of deadlock
private void Button_Click(object sender, EventArgs e)
{
    ProcessDataAsync().Wait(); // ❌ Very dangerous in UI context
}

// ASP.NET Controller - can cause thread pool starvation
public ActionResult GetData()
{
    var result = FetchDataAsync().Result; // ❌ Blocks request thread
    return Json(result);
}

// Library method - blocks caller unnecessarily
public string GetConfiguration()
{
    return LoadConfigAsync().GetAwaiter().GetResult(); // ❌ Forces synchronous behavior
}
```

## Correct async patterns

```csharp
// UI event handler - make it async
private async void Button_Click(object sender, EventArgs e)
{
    await ProcessDataAsync(); // ✅ Proper async UI pattern
}

// ASP.NET Controller - return Task
public async Task<ActionResult> GetData()
{
    var result = await FetchDataAsync(); // ✅ Truly async request handling
    return Json(result);
}

// Library method - keep it async
public async Task<string> GetConfigurationAsync()
{
    return await LoadConfigAsync(); // ✅ Maintains async all the way
}
```

## Related rules

- **EPC27**: Avoid async void methods (except for event handlers)
- **EPC31**: Do not return null for Task-like types
- **EPC33**: Do not use Thread.Sleep in async methods

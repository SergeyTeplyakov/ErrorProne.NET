# EPC31 - Do not return null for Task-like types

This analyzer detects when methods return null for Task-like types, which can cause `NullReferenceException` when the task is awaited.

## Description

The analyzer warns when methods with Task-like return types (Task, Task<T>, ValueTask, etc.) return null. Awaiting a null task will throw a `NullReferenceException`, which is unexpected behavior for async methods.

## Code that triggers the analyzer

```csharp
public class Example
{
    // Bad: returning null Task
    public Task ProcessAsync()
    {
        if (someCondition)
            return null; // ❌ EPC31 - This will cause NRE when awaited
        
        return DoWorkAsync();
    }
    
    // Bad: returning null Task<T>
    public Task<string> GetDataAsync()
    {
        if (noData)
            return null; // ❌ EPC31 - NRE when awaited
            
        return FetchDataAsync();
    }
    
    // Bad: conditional return with null
    public Task? MaybeProcessAsync()
    {
        return condition ? ProcessDataAsync() : null; // ❌ EPC31
    }
}
```

## How to fix

Return appropriate completed tasks instead of null:

```csharp
public class Example
{
    // Good: return completed task instead of null
    public Task ProcessAsync()
    {
        if (someCondition)
            return Task.CompletedTask; // ✅ Correct - Safe to await
        
        return DoWorkAsync();
    }
    
    // Good: return completed task with result
    public Task<string> GetDataAsync()
    {
        if (noData)
            return Task.FromResult<string>(null); // ✅ Correct - Or return default value
            
        return FetchDataAsync();
    }
    
    // Good: return appropriate default values
    public Task<int> GetCountAsync()
    {
        if (isEmpty)
            return Task.FromResult(0); // ✅ Correct - Return default value
            
        return CalculateCountAsync();
    }
}
```

For methods that might legitimately not have work to do:

```csharp
public class Example
{
    // Use Task.CompletedTask for void-like async methods
    public Task ProcessIfNeededAsync()
    {
        if (!needsProcessing)
            return Task.CompletedTask; // ✅ Correct
            
        return ActualProcessingAsync();
    }
    
    // Use Task.FromResult for methods with return values
    public Task<string> GetCachedOrFetchAsync(string key)
    {
        var cached = cache.Get(key);
        if (cached != null)
            return Task.FromResult(cached);
            
        return FetchFromServerAsync(key);
    }
    
    // For ValueTask, use default value
    public ValueTask<int> GetValueAsync()
    {
        if (hasValue)
            return new ValueTask<int>(value);
            
        return new ValueTask<int>(FetchValueAsync());
    }
}
```

## Why null tasks are problematic

- Awaiting null throws `NullReferenceException`
- Breaks the async/await pattern expectations
- Makes error handling inconsistent
- Can cause unexpected application crashes

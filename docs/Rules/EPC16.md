# EPC16 - Awaiting a result of a null-conditional expression will cause NullReferenceException

This analyzer detects dangerous patterns where awaiting the result of a null-conditional operator can cause a `NullReferenceException`.

## Description

The analyzer warns when code awaits the result of a null-conditional expression. When the left-hand side of the null-conditional operator is null, the expression returns null, and awaiting null will throw a `NullReferenceException`.

## Code that triggers the analyzer

```csharp
public async Task ProcessAsync()
{
    SomeService service = GetService(); // might return null
    
    // Dangerous: if service is null, this returns null, and awaiting null throws NRE
    await service?.ProcessAsync(); // ❌ EPC16
}
```

```csharp
public async Task<string> GetDataAsync()
{
    var client = GetHttpClient(); // might return null
    
    // This will throw NRE if client is null
    return await client?.GetStringAsync("http://example.com"); // ❌ EPC16
}
```

## How to fix

Check for null before awaiting, or use proper null handling:

```csharp
public async Task ProcessAsync()
{
    SomeService service = GetService();
    
    // Option 1: Check for null first
    if (service != null)
    {
        await service.ProcessAsync(); // ✅ Correct
    }
    
    // Option 2: Use null-coalescing with Task.CompletedTask
    await (service?.ProcessAsync() ?? Task.CompletedTask); // ✅ Correct
}
```

```csharp
public async Task<string> GetDataAsync()
{
    var client = GetHttpClient();
    
    // Check for null before using
    if (client == null)
    {
        return null; // or throw, or return default value
    }
    
    return await client.GetStringAsync("http://example.com"); // ✅ Correct
}
```

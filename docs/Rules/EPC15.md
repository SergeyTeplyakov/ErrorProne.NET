# EPC15 - ConfigureAwait(false) must be used

This analyzer detects missing `ConfigureAwait(false)` calls when the assembly is configured to require them.

## Description

The analyzer warns when `ConfigureAwait(false)` should be used but is missing. This typically applies to library code where you want to avoid deadlocks by not capturing the synchronization context.

## Code that triggers the analyzer

```csharp
public async Task ProcessAsync()
{
    // Missing ConfigureAwait(false) - this triggers the warning
    await SomeAsyncMethod();
    
    // Also missing ConfigureAwait(false)
    var result = await GetDataAsync();
}
```

## How to fix

Add `ConfigureAwait(false)` to all await expressions:

```csharp
public async Task ProcessAsync()
{
    // Add ConfigureAwait(false) to avoid capturing sync context
    await SomeAsyncMethod().ConfigureAwait(false);
    
    var result = await GetDataAsync().ConfigureAwait(false);
}
```

## When to use ConfigureAwait(false)

Use `ConfigureAwait(false)` in:
- Library code that doesn't need to return to the original synchronization context
- Background processing code
- Code that doesn't interact with UI elements

Don't use `ConfigureAwait(false)` in:
- UI event handlers
- Controller actions that need to return to the original context
- Code that accesses UI elements after the await

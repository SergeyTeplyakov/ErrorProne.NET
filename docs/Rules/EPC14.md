# EPC14 - ConfigureAwait(false) call is redundant

This analyzer detects redundant calls to `ConfigureAwait(false)` when the assembly is configured not to require them.

## Description

The analyzer provides information (not a warning) when `ConfigureAwait(false)` calls are redundant because the assembly has been configured to not capture the synchronization context automatically. This helps clean up unnecessary code.

## Code that triggers the analyzer

```csharp
public async Task ProcessAsync()
{
    // ConfigureAwait(false) is redundant when assembly is configured for it
    await SomeAsyncMethod().ConfigureAwait(false);
    
    // This is also redundant
    var result = await GetDataAsync().ConfigureAwait(false);
}
```

## How to fix

Remove the redundant `ConfigureAwait(false)` calls:

```csharp
public async Task ProcessAsync()
{
    // ConfigureAwait(false) behavior is already configured at assembly level
    await SomeAsyncMethod();
    
    var result = await GetDataAsync();
}
```

Note: This rule only applies when your assembly is configured to not capture synchronization context by default. The configuration is typically done through project settings or attributes.

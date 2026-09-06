# EPC17 - Avoid async-void delegates

This analyzer detects the use of async void delegates, which can cause application crashes due to unhandled exceptions.

## Description

The analyzer warns against using async void delegates. Unlike async void methods (which should only be used for event handlers), async void delegates are particularly dangerous because exceptions thrown in them cannot be caught and will crash the application.

## Code that triggers the analyzer

```csharp
public void SetupHandlers()
{
    // Dangerous: async void delegate
    SomeEvent += async () => // ❌ EPC17
    {
        await SomeAsyncMethod();
        // If this throws, it will crash the app
    };
    
    // Another dangerous pattern
    Task.Run(async () => // ❌ EPC17
    {
        await ProcessAsync();
        // Exceptions here are unhandled
    });
}
```

```csharp
public void RegisterCallback()
{
    // Async void delegate in callback
    RegisterCallback(async () => // ❌ EPC17
    {
        await DoWorkAsync();
    });
}
```

## How to fix

Use regular void-return method and make a blocking call to async method.

```csharp
public void SetupHandlers()
{
    // Safe: async Task delegate
    SomeEvent += (sender, e) =>
    {
        try
        {
            SomeAsyncMethod().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // Handle exception properly
            LogError(ex);
        }
    };}
```
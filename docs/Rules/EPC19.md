# EPC19 - Observe and Dispose a 'CancellationTokenRegistration' to avoid memory leaks

This analyzer detects when `CancellationTokenRegistration` objects are not being properly disposed, which can lead to memory leaks.

## Description

The analyzer warns when `CancellationToken.Register()` calls create `CancellationTokenRegistration` objects that are not being stored and disposed. If the cancellation token outlives the object that registered the callback, the registration can cause a memory leak.

## Code that triggers the analyzer

```csharp
public class MyService
{
    public void StartOperation(CancellationToken cancellationToken)
    {
        // Registration is not stored - potential memory leak
        cancellationToken.Register(() =>  // ❌ EPC19
        {
            Console.WriteLine("Operation cancelled");
        });
    }
}
```

```csharp
public async Task ProcessAsync(CancellationToken token)
{
    // Registration result is ignored
    token.Register(OnCancellation); // ❌ EPC19
    
    await DoWorkAsync();
}

private void OnCancellation()
{
    // Cleanup logic
}
```

## How to fix

Store the registration and dispose it when no longer needed:

```csharp
public class MyService : IDisposable
{
    private CancellationTokenRegistration _registration;
    
    public void StartOperation(CancellationToken cancellationToken)
    {
        // Store the registration
        _registration = cancellationToken.Register(() =>  // ✅ Correct
        {
            Console.WriteLine("Operation cancelled");
        });
    }
    
    public void Dispose()
    {
        // Dispose the registration to prevent memory leaks
        _registration.Dispose(); // ✅ Correct
    }
}
```

```csharp
public async Task ProcessAsync(CancellationToken token)
{
    // Store registration and dispose in finally block
    var registration = token.Register(OnCancellation); // ✅ Correct
    try
    {
        await DoWorkAsync();
    }
    finally
    {
        registration.Dispose(); // ✅ Correct
    }
}

// Or use using statement
public async Task ProcessAsync(CancellationToken token)
{
    using var registration = token.Register(OnCancellation); // ✅ Correct
    await DoWorkAsync();
    // registration automatically disposed here
}
```

## When this matters

This is especially important when:
- The cancellation token has a longer lifetime than the registering object
- You're registering callbacks in frequently called methods
- The registered callbacks capture references to large objects

# EPC37 - Do not validate arguments in async methods

This analyzer warns when public async methods validate arguments and throw exceptions. Such validation is not eager and exceptions are thrown when the task is awaited, which can lead to unexpected behavior.

## Description

When you validate arguments in async methods by throwing exceptions, the exceptions are not thrown immediately when the method is called. Instead, they are thrown when the returned `Task` is awaited. This behavior can be confusing and lead to bugs because:

1. **Delayed failure**: Argument validation failures are not discovered until the task is awaited, which may happen much later in the code
2. **Inconsistent behavior**: Synchronous methods validate arguments eagerly, but async methods do not
3. **Debugging difficulty**: Stack traces may not clearly show where the invalid argument was passed

This analyzer only reports diagnostics for public methods in public classes, as these are the API boundaries where eager validation is most important.

## Code that triggers the analyzer

❌ **Bad** - Argument validation in async methods:
```csharp
using System;
using System.Threading.Tasks;

public class FileService
{
    public async Task<string> ReadFileAsync(string fileName)
    {
        // ❌ EPC37: This exception won't be thrown until the task is awaited
        if (fileName == null) 
            throw new ArgumentNullException(nameof(fileName));
        
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty", nameof(fileName));

        return await File.ReadAllTextAsync(fileName);
    }

    public async Task ProcessDataAsync(int[] data)
    {
        // ❌ EPC37: Using modern validation methods still has the same problem
        ArgumentNullException.ThrowIfNull(data);
        
        foreach (var item in data)
        {
            await ProcessItemAsync(item);
        }
    }
}
```

## How to fix

There are several approaches to fix this issue:

✅ **Good** - Use wrapper method pattern:
```csharp
using System;
using System.Threading.Tasks;

public class FileService
{
    public Task<string> ReadFileAsync(string fileName)
    {
        // Validate arguments eagerly in the wrapper
        if (fileName == null) 
            throw new ArgumentNullException(nameof(fileName));
        
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty", nameof(fileName));

        // Call the actual async implementation
        return ReadFileAsyncCore(fileName);
    }

    private async Task<string> ReadFileAsyncCore(string fileName)
    {
        return await File.ReadAllTextAsync(fileName);
    }
}
```
✅ **Good** - Use local functions for cleaner code:
```csharp
using System;
using System.Threading.Tasks;

public class FileService
{
    public Task<string> ReadFileAsync(string fileName)
    {
        // Validate arguments eagerly
        if (fileName == null) 
            throw new ArgumentNullException(nameof(fileName));
        
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty", nameof(fileName));

        // Use local async function
        return ReadFileAsyncLocal();

        async Task<string> ReadFileAsyncLocal()
        {
            return await File.ReadAllTextAsync(fileName);
        }
    }
}
```

## When this rule doesn't apply

The analyzer only reports diagnostics when:
- The method is `async`
- The method is `public`
- The containing type and all parent types are `public`
- The exception is thrown before any `await` expressions in the method
- The exception is an `ArgumentException`, `ArgumentNullException`, `ArgumentOutOfRangeException`, or inherits from `ArgumentException`

❌ **No warning** - Private methods:
```csharp
public class FileService
{
    private async Task<string> ReadFileAsyncInternal(string fileName)
    {
        // No warning: private method
        if (fileName == null) throw new ArgumentNullException(nameof(fileName));
        return await File.ReadAllTextAsync(fileName);
    }
}
```

❌ **No warning** - Non-public classes:
```csharp
internal class FileService
{
    public async Task<string> ReadFileAsync(string fileName)
    {
        // No warning: internal class
        if (fileName == null) throw new ArgumentNullException(nameof(fileName));
        return await File.ReadAllTextAsync(fileName);
    }
}
```

❌ **No warning** - Synchronous methods:
```csharp
public class FileService
{
    public string ReadFile(string fileName)
    {
        // No warning: synchronous method validates arguments eagerly
        if (fileName == null) throw new ArgumentNullException(nameof(fileName));
        return File.ReadAllText(fileName);
    }
}
```

❌ **No warning** - Exceptions after await:
```csharp
public class FileService
{
    public async Task<string> ProcessFileAsync(string fileName)
    {
        var content = await File.ReadAllTextAsync(fileName);
        
        // No warning: this is not argument validation, it's business logic validation
        if (content.Length == 0)
            throw new ArgumentException("File is empty");
        
        return content.ToUpper();
    }
}
```

## Performance and behavior impact

The wrapper method pattern has minimal performance overhead and provides the expected eager validation behavior:

```csharp
// This will throw immediately
try 
{
    var task = fileService.ReadFileAsync(null); // Throws ArgumentNullException here
    await task;
}
catch (ArgumentNullException ex)
{
    // Exception caught immediately, not when awaiting
}

// VS the problematic async validation:
try 
{
    var task = problematicService.ReadFileAsync(null); // Returns a task
    await task; // ArgumentNullException thrown here when awaited
}
catch (ArgumentNullException ex)
{
    // Exception caught later, during await
}
```

## Examples in context

❌ **Bad** - API that fails unexpectedly:
```csharp
public class DataProcessor
{
    public async Task<ProcessResult> ProcessAsync(string data, CancellationToken cancellationToken)
    {
        // ❌ These validations won't fail until the task is awaited
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0) throw new ArgumentException("Data cannot be empty");
        
        await SomeAsyncOperation(cancellationToken);
        return new ProcessResult(data);
    }
}

// Usage that demonstrates the problem:
var processor = new DataProcessor();
var tasks = new List<Task<ProcessResult>>();

// All these calls succeed and return tasks, even with invalid arguments!
tasks.Add(processor.ProcessAsync(null, CancellationToken.None));  
tasks.Add(processor.ProcessAsync("", CancellationToken.None));    
tasks.Add(processor.ProcessAsync("valid", CancellationToken.None));

// Only when we await do we discover the validation failures
foreach (var task in tasks)
{
    try 
    {
        var result = await task; // Failures happen here, not at call site
    }
    catch (ArgumentException ex)
    {
        Console.WriteLine($"Validation failed: {ex.Message}");
    }
}
```

✅ **Good** - API that fails eagerly:
```csharp
public class DataProcessor
{
    public Task<ProcessResult> ProcessAsync(string data, CancellationToken cancellationToken)
    {
        // ✅ Validate arguments eagerly
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0) throw new ArgumentException("Data cannot be empty");
        
        return ProcessAsyncCore(data, cancellationToken);
    }

    private async Task<ProcessResult> ProcessAsyncCore(string data, CancellationToken cancellationToken)
    {
        await SomeAsyncOperation(cancellationToken);
        return new ProcessResult(data);
    }
}

// Usage with eager validation:
var processor = new DataProcessor();
var tasks = new List<Task<ProcessResult>>();

try 
{
    // These calls throw immediately if arguments are invalid
    tasks.Add(processor.ProcessAsync(null, CancellationToken.None));  // Throws here!
}
catch (ArgumentNullException ex)
{
    Console.WriteLine($"Invalid argument caught immediately: {ex.Message}");
}
```

## See also

- [Async/await best practices](https://docs.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)
- [Argument validation in async methods - Stephen Cleary](https://blog.stephencleary.com/2014/12/async-oop-2-constructors.html)

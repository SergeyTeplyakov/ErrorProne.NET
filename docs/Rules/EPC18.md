# EPC18 - A task instance is implicitly converted to a string

This analyzer detects when a `Task` instance is implicitly converted to a string, which often indicates a missing `await` keyword.

## Description

The analyzer warns when a `Task` object is being implicitly converted to a string. This usually happens when developers forget to await a task and try to use it directly in string operations. The resulting string will be the Task's type name rather than the actual result.

## Code that triggers the analyzer

```csharp
public async Task<string> GetDataAsync()
{
    return "Hello World";
}

public void ProcessData()
{
    // Missing await - Task<string> converted to string
    string result = GetDataAsync(); // ❌ EPC18 - This will be "System.Threading.Tasks.Task`1[System.String]"
    
    // In string interpolation
    Console.WriteLine($"Result: {GetDataAsync()}"); // ❌ EPC18 - Prints task type, not result
    
    // In concatenation
    string message = "Data: " + GetDataAsync(); // ❌ EPC18 - Concatenates with task type
}
```

## How to fix

Add the `await` keyword to get the actual result:

```csharp
public async Task<string> GetDataAsync()
{
    return "Hello World";
}

public async Task ProcessData()
{
    // Properly await the task
    string result = await GetDataAsync(); // ✅ Correct - Now gets "Hello World"
    
    // In string interpolation
    Console.WriteLine($"Result: {await GetDataAsync()}"); // ✅ Correct - Prints actual result
    
    // In concatenation
    string message = "Data: " + await GetDataAsync(); // ✅ Correct - Concatenates with actual result
}
```
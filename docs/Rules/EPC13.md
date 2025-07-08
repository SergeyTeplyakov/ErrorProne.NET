# EPC13 - Suspiciously unobserved result

This analyzer detects when method return values that should typically be observed are being ignored.

## Description

The analyzer warns when the result of a method call is not being used, particularly for methods that return important values that should be observed. This helps catch cases where developers might have forgotten to use the return value of a method.

## Code that triggers the analyzer

```csharp
public class Example
{
    public void ProcessData()
    {
        // Return value is ignored - this triggers the warning
        GetImportantValue(); // ❌ EPC13
        
        // String methods return new strings but result is ignored
        "hello".ToUpper(); // ❌ EPC13
        
        // Collection methods that return new collections
        list.Where(x => x > 0); // ❌ EPC13
    }
    
    public string GetImportantValue()
    {
        return "important data";
    }
}
```

## How to fix

Observe (use) the return values:

```csharp
public class Example
{
    public void ProcessData()
    {
        // Store the result in a variable
        var importantValue = GetImportantValue(); // ✅ Correct
        
        // Use the result directly
        var upperText = "hello".ToUpper(); // ✅ Correct
        Console.WriteLine(upperText);
        
        // Chain operations or store result
        var filteredList = list.Where(x => x > 0).ToList(); // ✅ Correct
    }
    
    public string GetImportantValue()
    {
        return "important data";
    }
}
```

If you intentionally want to ignore the result, you can use discard:

```csharp
_ = GetImportantValue(); // Explicitly ignore the result
```

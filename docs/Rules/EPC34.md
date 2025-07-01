# EPC34 - Method return value marked with MustUseResultAttribute must be used

This analyzer detects when methods marked with `MustUseResultAttribute` have their return values ignored, which indicates the result should always be observed.

## Description

The analyzer warns when the return value of a method decorated with `MustUseResultAttribute` is not used. This attribute indicates that the method's return value contains important information that should always be observed by the caller.

## Code that triggers the analyzer

```csharp
[System.AttributeUsage(System.AttributeTargets.Method)]
public class MustUseResultAttribute : System.Attribute { }

public class ValidationService
{
    [MustUseResult]
    public bool ValidateInput(string input)
    {
        return !string.IsNullOrEmpty(input) && input.Length > 3;
    }
    
    [MustUseResult]
    public ValidationResult CheckData(object data)
    {
        return new ValidationResult(data != null, "Data cannot be null");
    }
}

public class Example
{
    public void ProcessData(string input, object data)
    {
        var validator = new ValidationService();
        
        // Bad: ignoring return value of method marked with MustUseResult
        validator.ValidateInput(input);
        
        // Also bad: not using the validation result
        validator.CheckData(data);
        
        // Proceeding without checking validation results - dangerous!
        DoSomethingWithData(input, data);
    }
}
```

## How to fix

Always observe and use the return values:

```csharp
public class Example
{
    public void ProcessData(string input, object data)
    {
        var validator = new ValidationService();
        
        // Good: checking the validation result
        bool isValid = validator.ValidateInput(input);
        if (!isValid)
        {
            throw new ArgumentException("Invalid input");
        }
        
        // Good: using the validation result
        var result = validator.CheckData(data);
        if (!result.IsValid)
        {
            throw new ArgumentException(result.ErrorMessage);
        }
        
        // Now safe to proceed
        DoSomethingWithData(input, data);
    }
}
```

In conditional checks:

```csharp
public class Example
{
    public void ProcessDataConditionally(string input)
    {
        var validator = new ValidationService();
        
        // Good: using result in conditional
        if (validator.ValidateInput(input))
        {
            ProcessValidInput(input);
        }
        else
        {
            HandleInvalidInput(input);
        }
    }
    
    public void ProcessWithEarlyReturn(object data)
    {
        var validator = new ValidationService();
        
        // Good: using result for early return
        var result = validator.CheckData(data);
        if (!result.IsValid)
            return; // Early exit on validation failure
            
        ProcessValidData(data);
    }
}
```

If you intentionally want to ignore the result (rare cases):

```csharp
public class Example
{
    public void ProcessData(string input)
    {
        var validator = new ValidationService();
        
        // Explicitly ignore result with discard (use with caution)
        _ = validator.ValidateInput(input);
        
        // Better: add comment explaining why result is ignored
        _ = validator.ValidateInput(input); // Validation is optional in this context
    }
}
```

For async methods with MustUseResult:

```csharp
public class AsyncValidator
{
    [MustUseResult]
    public async Task<bool> ValidateAsync(string input)
    {
        await Task.Delay(100); // Simulate async validation
        return !string.IsNullOrEmpty(input);
    }
}

public class Example
{
    public async Task ProcessAsync(string input)
    {
        var validator = new AsyncValidator();
        
        // Good: awaiting and using the result
        bool isValid = await validator.ValidateAsync(input);
        if (!isValid)
        {
            throw new ArgumentException("Validation failed");
        }
        
        await ProcessValidInputAsync(input);
    }
}
```

## When to use MustUseResultAttribute

Apply this attribute to methods where:
- Return value indicates success/failure
- Return value contains error information
- Ignoring the result could lead to bugs
- The method's primary purpose is to return information
- Return value affects subsequent program behavior

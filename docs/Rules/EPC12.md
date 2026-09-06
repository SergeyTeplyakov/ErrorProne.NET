# EPC12 - Suspicious exception handling: only the 'Message' property is observed in the catch block

This analyzer detects catch blocks where only the `Message` property of an exception is used, which often indicates incomplete exception handling.

## Description

The analyzer warns when a catch block only accesses the `Message` property of an exception without observing the exception object itself or its other important properties like `InnerException`. This is suspicious because the `Message` property often contains generic information, while the actual useful data is in the exception object or its inner exceptions.

## Code that triggers the analyzer

```csharp
try
{
    // Some risky operation
    SomeMethod();
}
catch (Exception ex)
{
    // Only using ex.Message - this triggers the warning
    Console.WriteLine(ex.Message); // ❌ EPC12
}
```


## How to fix

Use the full exception object or access additional properties like `InnerException`:

```csharp
try
{
    SomeMethod();
}
catch (Exception ex)
{
    // Log the full exception object
    Console.WriteLine(ex.ToString()); // ✅ Correct
    // Or access the exception object directly
    LogException(ex); // ✅ Correct
}
```

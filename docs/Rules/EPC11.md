# EPC11 - Suspicious equality implementation

This analyzer detects suspicious implementations of `Equals` methods that may not be working as intended.

## Description

The analyzer warns when an `Equals` method implementation appears suspicious, typically when the method doesn't use any instance members or compares against other instances properly.

## Code that triggers the analyzer

```csharp
public class MyClass
{
    public override bool Equals(object obj)
    {
        // Suspicious: only using static members or not using 'this' instance
        return SomeStaticProperty == 42;
    }
}
```

```csharp
public class Person
{
    public string Name { get; set; }
    
    public override bool Equals(object obj)
    {
        // Suspicious: parameter 'obj' is never used
        return this.Name == "test";
    }
}
```

## How to fix

Implement `Equals` method properly by:

1. Using the parameter that's passed in
2. Checking instance members against the other object's members
3. Following the standard equality pattern

```csharp
public class Person
{
    public string Name { get; set; }
    
    public override bool Equals(object obj)
    {
        if (obj is Person other)
        {
            return this.Name == other.Name;
        }
        return false;
    }
}
```

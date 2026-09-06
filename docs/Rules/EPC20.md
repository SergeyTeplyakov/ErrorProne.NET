# EPC20 - Avoid using default ToString implementation

This analyzer detects when the default `ToString()` implementation is being used, which rarely provides meaningful output.

## Description

The analyzer warns when the default `ToString()` implementation is used. The default implementation simply returns the type name, which is rarely what you want when converting an object to string. This often indicates that a custom `ToString()` implementation should be provided.

## Code that triggers the analyzer

```csharp
public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
    // No custom ToString() implementation
}

public void Example()
{
    var person = new Person { Name = "John", Age = 30 };
    
    // Using default ToString() - outputs "Person" instead of useful info
    Console.WriteLine(person.ToString()); // ❌ EPC20
    
    // Implicit ToString() call
    string personString = person; // ❌ EPC20
    
    // In string interpolation
    Console.WriteLine($"Person: {person}"); // ❌ EPC20
}
```

## How to fix

Implement a meaningful `ToString()` method:

```csharp
public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
    
    // Custom ToString() implementation
    public override string ToString() // ✅ Correct
    {
        return $"Person(Name: {Name}, Age: {Age})";
    }
}

public void Example()
{
    var person = new Person { Name = "John", Age = 30 };
    
    // Now outputs meaningful information: "Person(Name: John, Age: 30)"
    Console.WriteLine(person.ToString());
    
    string personString = person; // Also uses custom ToString()
    
    Console.WriteLine($"Person: {person}"); // Shows useful data
}
```

Alternative approaches:

```csharp
// Use string interpolation directly instead of ToString()
Console.WriteLine($"Name: {person.Name}, Age: {person.Age}");

// Use a dedicated formatting method
public string ToDisplayString()
{
    return $"{Name} ({Age} years old)";
}

// Use record types which provide automatic ToString()
public record Person(string Name, int Age);
```

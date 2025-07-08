# EPC28 - Do not use ExcludeFromCodeCoverage on partial classes

This analyzer detects when `ExcludeFromCodeCoverageAttribute` is applied to partial classes, which can lead to inconsistent code coverage results.

## Description

The analyzer warns when `ExcludeFromCodeCoverageAttribute` is applied to partial classes. This can cause inconsistent behavior because code coverage tools might not handle partial classes with this attribute correctly, leading to unreliable coverage reports.

## Code that triggers the analyzer

```csharp
using System.Diagnostics.CodeAnalysis;

// Bad: ExcludeFromCodeCoverage on partial class
[ExcludeFromCodeCoverage] // ❌ EPC28
public partial class MyPartialClass
{
    public void Method1()
    {
        // This might or might not be excluded from coverage
    }
}

// Another part of the same class
public partial class MyPartialClass
{
    public void Method2()
    {
        // Coverage behavior is inconsistent
    }
}
```

```csharp
// Also problematic: one part has the attribute
[ExcludeFromCodeCoverage] // ❌ EPC28
public partial class DataClass
{
    // Part 1
}

public partial class DataClass
{
    // Part 2 - inconsistent coverage behavior
}
```

## How to fix

Apply the attribute to specific members instead of the partial class:

```csharp
public partial class MyPartialClass
{
    [ExcludeFromCodeCoverage] // ✅ Correct
    public void Method1()
    {
        // Explicitly excluded from coverage
    }
    
    public void Method2() // ✅ Correct
    {
        // Included in coverage
    }
}

public partial class MyPartialClass
{
    [ExcludeFromCodeCoverage] // ✅ Correct
    public void Method3()
    {
        // Explicitly excluded from coverage
    }
}
```

Or if the entire class should be excluded, use a non-partial class:

```csharp
// If you need to exclude the entire class, make it non-partial
[ExcludeFromCodeCoverage]
public class MyClass
{
    public void Method1()
    {
        // Consistently excluded from coverage
    }
    
    public void Method2()
    {
        // Also excluded
    }
}
```

Or extract the parts that need to be excluded:

```csharp
// Keep main class without attribute
public partial class MyPartialClass
{
    public void ImportantMethod()
    {
        // Included in coverage
    }
}

// Separate class for excluded code
[ExcludeFromCodeCoverage]
public class MyGeneratedClass
{
    public void GeneratedMethod()
    {
        // Excluded from coverage
    }
}
```

## Why this matters

- Code coverage tools may inconsistently handle partial classes with this attribute
- Some tools might exclude all parts, others might exclude none
- Can lead to unreliable coverage metrics
- Makes it harder to track actual test coverage

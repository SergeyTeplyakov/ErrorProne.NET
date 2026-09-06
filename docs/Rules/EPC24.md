# EPC24 - A hash table "unfriendly" type is used as the key in a hash table

This analyzer detects when structs with default implementations of `Equals` and `GetHashCode` are used as keys in hash tables, which can cause severe performance issues.

## Description

The analyzer warns when a struct that relies on the default ValueType implementation of `Equals` or `GetHashCode` is used as a key in hash-based collections like `Dictionary<TKey, TValue>`, `HashSet<T>`, etc. The default implementations use reflection and are very slow.

## Code that triggers the analyzer

```csharp
public struct Point
{
    public int X { get; set; }
    public int Y { get; set; }
    // No custom Equals or GetHashCode implementation
}

public void Example()
{
    // Using struct with default Equals/GetHashCode as dictionary key
    var pointData = new Dictionary<Point, string>(); // ❌ EPC24
    
    var point = new Point { X = 1, Y = 2 };
    pointData[point] = "First point"; // Performance issue!
    
    // HashSet also affected
    var pointSet = new HashSet<Point>(); // ❌ EPC24
    pointSet.Add(point); // Performance issue!
}
```

## How to fix

Implement custom `Equals` and `GetHashCode` methods:

```csharp
public struct Point : IEquatable<Point>
{
    public int X { get; set; }
    public int Y { get; set; }
    
    // Custom Equals implementation
    public bool Equals(Point other) // ✅ Correct
    {
        return X == other.X && Y == other.Y;
    }
    
    public override bool Equals(object obj) // ✅ Correct
    {
        return obj is Point other && Equals(other);
    }
    
    // Custom GetHashCode implementation
    public override int GetHashCode() // ✅ Correct
    {
        return HashCode.Combine(X, Y);
    }
}
```

Or use a record struct (C# 10+):

```csharp
// Record structs automatically implement Equals and GetHashCode
public record struct Point(int X, int Y);

public void Example()
{
    var pointData = new Dictionary<Point, string>();
    var point = new Point(1, 2);
    pointData[point] = "First point"; // Now efficient!
}
```

## Performance Impact

The default ValueType implementations:
- Use reflection to compare all fields
- Are significantly slower than custom implementations
- Can cause major performance bottlenecks in hash-based collections

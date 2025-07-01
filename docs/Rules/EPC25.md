# EPC25 - Avoid using default Equals or HashCode implementation from structs

This analyzer detects when the default `ValueType.Equals` or `ValueType.GetHashCode` implementations are being used directly, which can cause performance issues.

## Description

The analyzer warns when code directly calls the default `Equals` or `GetHashCode` implementations from `ValueType`. These implementations use reflection and are significantly slower than custom implementations.

## Code that triggers the analyzer

```csharp
public struct MyStruct
{
    public int Value1 { get; set; }
    public int Value2 { get; set; }
    // No custom Equals or GetHashCode
}

public void Example()
{
    var struct1 = new MyStruct { Value1 = 1, Value2 = 2 };
    var struct2 = new MyStruct { Value1 = 1, Value2 = 2 };
    
    // Using default ValueType.Equals - slow!
    bool areEqual = struct1.Equals(struct2); // ❌ EPC25
    
    // Using default ValueType.GetHashCode - slow!
    int hash = struct1.GetHashCode(); // ❌ EPC25
    
    // In collections this causes performance issues
    var dictionary = new Dictionary<MyStruct, string>(); // ❌ EPC25
    dictionary[struct1] = "value"; // Triggers slow hash operations
}
```

## How to fix

Implement custom `Equals` and `GetHashCode`:

```csharp
public struct MyStruct : IEquatable<MyStruct>
{
    public int Value1 { get; set; }
    public int Value2 { get; set; }
    
    public bool Equals(MyStruct other) // ✅ Correct
    {
        return Value1 == other.Value1 && Value2 == other.Value2;
    }
    
    public override bool Equals(object obj)
    {
        return obj is MyStruct other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(Value1, Value2);
    }
    
    // Optional: implement operators for consistency
    public static bool operator ==(MyStruct left, MyStruct right)
    {
        return left.Equals(right);
    }
    
    public static bool operator !=(MyStruct left, MyStruct right)
    {
        return !left.Equals(right);
    }
}
```

Or use record struct (C# 10+):

```csharp
// Record structs automatically provide efficient implementations
public record struct MyStruct(int Value1, int Value2);

public void Example()
{
    var struct1 = new MyStruct(1, 2);
    var struct2 = new MyStruct(1, 2);
    
    // Now uses efficient generated implementations
    bool areEqual = struct1.Equals(struct2);
    int hash = struct1.GetHashCode();
}
```

## Performance considerations

- Default `ValueType.Equals`: Uses reflection to compare all fields
- Default `ValueType.GetHashCode`: Uses reflection and can cause hash collisions
- Custom implementations: Direct field access, much faster
- Record structs: Compiler-generated efficient implementations

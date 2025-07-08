# EPC23 - Avoid using Enumerable.Contains on HashSet<T>

This analyzer detects inefficient use of `Enumerable.Contains` on `HashSet` collections where the instance `Contains` method would be more efficient.

## Description

The analyzer warns when `Enumerable.Contains` is used on HashSet or other set collections. This results in a linear O(n) search instead of the O(1) hash-based lookup that the HashSet's instance `Contains` method provides.

## Code that triggers the analyzer

```csharp
using System.Linq;

public void Example()
{
    var hashSet = new HashSet<string> { "apple", "banana", "cherry" };
    
    // Inefficient: uses Enumerable.Contains (O(n) linear search)
    bool found = hashSet.Contains("apple", StringComparer.OrdinalIgnoreCase); // ❌ EPC23
    
    // Also inefficient when using Enumerable.Contains explicitly
    bool found2 = Enumerable.Contains(hashSet, "banana"); // ❌ EPC23
}
```

## How to fix

Use the HashSet's instance `Contains` method:

```csharp
public void Example()
{
    var hashSet = new HashSet<string> { "apple", "banana", "cherry" };
    
    // Efficient: uses HashSet.Contains (O(1) hash lookup)
    bool found = hashSet.Contains("apple"); // ✅ Correct
    
    // If you need custom comparison, create HashSet with comparer
    var caseInsensitiveSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "apple", "banana", "cherry"
    };
    bool found2 = caseInsensitiveSet.Contains("APPLE"); // ✅ Correct - O(1) with custom comparer
}
```

Or if you need the comparer for a specific search:

```csharp
public void Example()
{
    var hashSet = new HashSet<string> { "apple", "banana", "cherry" };
    
    // Convert to appropriate collection for the comparer you need
    var lookup = hashSet.ToLookup(x => x, StringComparer.OrdinalIgnoreCase);
    bool found = lookup.Contains("APPLE");
    
    // Or use a Dictionary if you need case-insensitive lookups frequently
    var dict = hashSet.ToDictionary(x => x, x => true, StringComparer.OrdinalIgnoreCase);
    bool found2 = dict.ContainsKey("APPLE");
}
```

## Performance Impact

- `Enumerable.Contains`: O(n) - scans through all elements
- `HashSet.Contains`: O(1) - direct hash-based lookup
- For large collections, this can be a significant performance difference

# EPC39 - Same enumerable iterated more than once inside a loop

Detects three patterns where the *same* `IEnumerable` is iterated more than once inside an outer loop,
producing O(N²) (or worse) behavior.

## Description

Iterating the same source twice inside a loop turns an O(N) walk into an O(N·M) one. When the inner pass
is a deferred LINQ chain, every outer iteration also re-runs every chained filter / projection.

The analyzer recognizes three sub-patterns and reports each with a tailored message:

### Q1 — Same-symbol nested enumeration

```csharp
foreach (var x in coll)
{
    var n = coll.Count(); // ❌ EPC39 — Count() re-iterates the entire collection on every step
}
```

### Q2 — Deferred IEnumerable<T> re-enumerated in a loop

```csharp
IEnumerable<int> q = source.Where(x => x > 0);
foreach (var i in outer)
{
    var c = q.Count(); // ❌ EPC39 — Where(...) re-runs every iteration
}
```

### Q4 — Nested foreach over the same source

```csharp
foreach (var x in coll)
{
    foreach (var y in coll) { ... } // ❌ EPC39 — O(N²)
}
```

## How to fix

### Q1 / Q2 — materialize once, or use a different shape

```csharp
var arr = coll.ToArray(); // or .ToList()
foreach (var x in arr)
{
    var n = arr.Length; // O(1)
}
```

If the inner work is genuinely "count every element matching X", pre-compute it outside the loop or replace
the loop body with a single LINQ aggregation that performs one pass.

### Q4 — symmetric problems usually benefit from a hash join

```csharp
var lookup = new HashSet<int>(coll);
foreach (var x in coll)
{
    if (lookup.Contains(-x)) { ... }
}
```

## When NOT to suppress

If the data set is *known* to be small (say, ≤ 16 elements) and the loop is on a hot path with a tight
cache footprint, a linear scan can actually be faster than a HashSet lookup. In those cases prefer to
suppress at the call site with a comment explaining the choice:

```csharp
#pragma warning disable EPC39 // List is bounded to <=8 elements; HashSet has higher constant factor.
```

## Patterns intentionally NOT flagged

- Calling `List<T>.Contains`, `Array.IndexOf`, `List<T>.FindIndex`, etc. inside a loop on a collection
  that is **not** the loop's source. The cost is O(N·M), but in real codebases these patterns are
  frequently correct on small inputs and previously produced too much noise. Use
  [CA1851](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1851)
  if you want broader "any double-enumeration" coverage, or [EPC23](EPC23.md) for the specific
  `HashSet.Contains`-via-LINQ misuse.
- `ImmutableArray<T>.Contains` and `Span<T>.IndexOf` — they don't allocate and are typically used on
  tiny inputs.

## Limitations (v2)

- Analysis is scoped to a single method body. Helper calls that internally enumerate are not tracked.
- Q1/Q2 LINQ root resolution stops at the first non-chain call. A chain like
  `GetItems().Where(...).Count()` is not flagged unless the root is a local/parameter (because resolving
  the source through arbitrary method bodies is unsound).

## See also

- [CA1851: Possible multiple enumerations of `IEnumerable` collection](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1851) — the general-purpose rule. EPC39 is more aggressive in the *quadratic* dimension (it fires inside loops) but narrower than CA1851 in scope (it only fires when the source is the same enumerable).
- [EPC38](EPC38.md) — re-enumeration of `IEnumerable<Task>` (correctness bug).
- [EPC23](EPC23.md) — `Enumerable.Contains` on a `HashSet<T>` (a related "wrong tool for the job" pattern).

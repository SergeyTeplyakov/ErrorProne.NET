# EPC40 - Multiple enumeration of deferred query passed to private method

Detects a cross-method variant of the "multiple enumeration" bug that CA1851 cannot diagnose:
a private method enumerates its `IEnumerable<T>` parameter more than once **and** a caller passes a
deferred LINQ query (or iterator method, or `Enumerable.Range/Repeat/Empty`). Every enumeration
re-runs the entire query.

The rule is restricted to `private` methods because callers form a *closed world* within the
compilation — we can enumerate them all and decide statically whether any of them passes a deferred
source. For public/internal methods this would be undecidable.

## When the rule fires

A diagnostic is produced at the **call site** when **all** of these conditions hold:

1. The target method is `private`.
2. The target method takes one or more `IEnumerable<T>` (or non-generic `IEnumerable`) parameters.
3. The target method body enumerates that parameter **≥ 2 times** (counting `foreach`, LINQ
   enumerating methods, and `Task.WhenAll(items)`/`WaitAll`/etc.).
4. The total number of call sites for the method is **≤ 3** (configurable cap; above the cap the
   rule conservatively gives up).
5. The argument expression at *this* call site is a **deferred query**, determined by walking up to
   **3 hops** through local declarations in the caller's body. A "deferred query" is one of:
   - A LINQ chain method call (`Where`, `Select`, `OrderBy`, `SelectMany`, `Concat`, `Take`, ...).
   - `Enumerable.Range`, `Enumerable.Repeat`, or `Enumerable.Empty`.
   - An invocation of an **iterator method** (a method with `yield return` / `yield break` in its
     body).

## Examples

### ❌ Bad

```csharp
private int Helper(IEnumerable<int> items) {
    var c = items.Count();
    return c + items.Sum();           // <-- second enumeration
}

void Caller(IEnumerable<int> src) {
    Helper(src.Where(x => x > 0));    // ❌ EPC40 — deferred query, Helper iterates twice
}
```

### ❌ Bad — iterator producer

```csharp
static IEnumerable<int> Produce() {
    yield return 1;
    yield return 2;
}

void Caller() {
    Helper(Produce());                // ❌ EPC40 — iterator method
}
```

### ❌ Bad — 2-hop provenance

```csharp
void Caller(IEnumerable<int> src) {
    var step1 = src.Where(x => x > 0);
    var step2 = step1;
    Helper(step2);                    // ❌ EPC40 — walked 2 hops back to .Where(...)
}
```

### ✅ Good — materialized argument

```csharp
void Caller(IEnumerable<int> src) {
    Helper(src.Where(x => x > 0).ToList());  // breaks the deferred chain
}
```

### ✅ Good — change the parameter type

```csharp
private int Helper(IReadOnlyList<int> items) {  // signals intent + lets you index/Count in O(1)
    var c = items.Count;
    var sum = 0;
    foreach (var x in items) sum += x;
    return c + sum;
}
```

### ✅ Not flagged — list/array caller

```csharp
void Caller(List<int> src) {
    Helper(src);   // List<T> is materialized; re-enumeration is cheap
}
```

### ✅ Not flagged — caller forwards its own parameter

```csharp
void Caller(IEnumerable<int> src) {
    Helper(src);   // We can't tell whether 'src' itself is a query without inter-procedural analysis
}
```

This is intentional — chasing provenance further would require inter-procedural dataflow, which
explodes quickly. If the *immediate* caller is itself private, see the **transitivity** note below.

## How to fix

Pick the right tool for the situation:

| Situation | Fix |
| --- | --- |
| Helper genuinely needs Count + foreach | Change the parameter type to `IReadOnlyList<T>` / `IReadOnlyCollection<T>` / `T[]` |
| Helper genuinely needs to enumerate twice | Call `items = items.ToList()` at the top of the helper, or materialize at the caller |
| Helper can be rewritten as one pass | Restructure to a single enumeration (often the best fix) |

Materializing at the **caller** preserves the helper's signature and makes the cost explicit to readers:

```csharp
Helper(src.Where(x => x > 0).ToList());
```

Materializing at the **callee** localizes the fix:

```csharp
private int Helper(IEnumerable<int> items) {
    var list = items.ToList();
    return list.Count + list.Sum();
}
```

## Limitations (v1)

- **Scope is `private` only.** `internal` methods are excluded for now because consumers under
  `InternalsVisibleTo` can break the closed-world assumption. May be expanded in v1.1 after dogfooding.
- **Caller cap = 3.** If a private method has more than 3 call sites, the rule does not fire. Such
  methods are effectively public within the assembly and the signal-to-noise drops.
- **Provenance hops = 3.** Chains longer than 3 local declarations are not followed.
- **No transitivity.** If `A` calls private `B` calls private `C`, and `C` enumerates twice and the
  call from `B` to `C` forwards a deferred parameter of `B`, this rule does *not* propagate the
  warning back to `A`. Transitive analysis is plausible but out of scope for v1.
- **No type-flow.** A `Func<IEnumerable<int>>` returning a query, invoked at the call site, is not
  resolved through the delegate.

## See also

- [CA1851: Possible multiple enumerations of `IEnumerable` collection](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1851) — same hazard, single-method scope only.
- [EPC38](EPC38.md) — `IEnumerable<Task>` re-enumeration (correctness bug variant).
- [EPC39](EPC39.md) — quadratic enumeration inside a loop (performance bug variant).

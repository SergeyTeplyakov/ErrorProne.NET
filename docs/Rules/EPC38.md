# EPC38 - Do not re-enumerate `IEnumerable<Task>`

Detects when a deferred `IEnumerable<Task>` / `IEnumerable<Task<T>>` is observed (enumerated) more than once
inside the same method body.

## Description

The standard way to "fan out" async work in .NET is to build a sequence of tasks from some input and then
wait for all of them:

```csharp
var tasks = ids.Select(async id => await DoWorkAsync(id));
await Task.WhenAll(tasks);
```

This *looks* like it produces a stable collection, but `Select` returns a **deferred** `IEnumerable<Task<T>>`:
every enumeration restarts the projection and creates a fresh set of tasks. So this seemingly innocent code:

```csharp
var tasks = ids.Select(async id => await DoWorkAsync(id));
await Task.WhenAll(tasks);
foreach (var t in tasks) // ❌ EPC38
{
    Process(await t);
}
```

…does **not** process the results of the tasks we awaited. It starts a brand-new set of `DoWorkAsync` calls,
awaits each one again, and processes those. The work is duplicated, side effects fire twice, and timing-
sensitive behavior breaks in confusing ways.

Materializing the sequence up front fixes the bug:

```csharp
var tasks = ids.Select(async id => await DoWorkAsync(id)).ToArray();
await Task.WhenAll(tasks);
foreach (var t in tasks) // ✅ tasks is Task<T>[] — safe to re-enumerate
{
    Process(await t);
}
```

The analyzer flags **any** symbol whose static type is `IEnumerable<Task>`, `IEnumerable<Task<T>>`,
`IEnumerable<ValueTask>`, or `IEnumerable<ValueTask<T>>` that is enumerated more than once inside the same
method body. Both observations matter — the rule does not require the first one to be `Task.WhenAll`.

## Code that triggers the analyzer

```csharp
async Task FooAsync(IEnumerable<int> ids)
{
    var tasks = ids.Select(async id => { await Task.Delay(1); return id; });
    await Task.WhenAll(tasks);
    foreach (var t in tasks) { await t; } // ❌ EPC38
}

void Counts(IEnumerable<Task<int>> tasks)
{
    var count = tasks.Count();
    var sum   = tasks.Sum(t => t.Result); // ❌ EPC38
}

async Task ManualAsync(IEnumerable<Task> tasks)
{
    foreach (var t in tasks) { await t; }
    foreach (var t in tasks) { _ = t.Status; } // ❌ EPC38 (strict superset case)
}
```

## How to fix

Materialize the sequence once and use the concrete collection from then on:

```csharp
async Task FooAsync(IEnumerable<int> ids)
{
    var tasks = ids.Select(async id => { await Task.Delay(1); return id; }).ToArray();
    await Task.WhenAll(tasks);
    foreach (var t in tasks) { await t; } // ✅ Task<int>[] — safe
}
```

`ToList()`, `ToArray()`, and similar materializing operators all work. If the source is a parameter and you
control the API, prefer to take `IReadOnlyList<Task<T>>` or `Task<T>[]` instead of `IEnumerable<Task<T>>`,
which both communicates intent and makes the rule fall silent.

## When NOT to suppress

In almost every case this is a real bug. Suppressing it usually means you've convinced yourself the producer
is *not* deferred — in which case the right fix is to change the static type of the variable to the
materialized type (`IReadOnlyList<Task<T>>`, `Task<T>[]`, etc.) so future readers don't have to make the
same leap.

## Limitations (v1)

- Analysis is scoped to a single method body. The rule does not track tasks passed across method boundaries.
- Field and property accesses are not tracked.
- Lambdas / local functions are analyzed as independent scopes.
- Re-assignment (`tasks = freshSource;`) resets tracking from that point onward.

## See also

- [CA1851: Possible multiple enumerations of `IEnumerable` collection](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1851) — the general-purpose rule. EPC38 specifically targets the task-producing case where re-enumeration is almost always a bug, not just a performance issue.
- [EPC39](EPC39.md) — quadratic enumeration inside a loop.

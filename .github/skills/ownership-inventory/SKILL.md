---
name: ownership-inventory
description: >-
  Inventory IDisposable and IAsyncDisposable implementations and trace ownership
  across C# producers and consumers. Use to find inconsistent disposal of results,
  critical resource types, ownership-transfer boundaries, and adoption candidates.
---

# Inventory disposable ownership

## Define and measure coverage

Start from tracked source and project membership. Exclude generated outputs,
build caches, secrets, deployment data, and unrelated languages. Classify
production, test-only, generated, and vendored code separately.
Account for case-varied project extensions. Inspect provenance before excluding
a directory merely named `Output`: it can contain real source. A fake in a
shipping-library folder is still a fake, not proof of production ownership.

Prefer an existing semantic index or Roslyn symbols for type relationships and
references. If only syntax/search is available, label the output accordingly.
Search is a candidate generator, not evidence that two callers invoke the same API.
Before starting an indexer or language server in a read-only audit, check whether
it triggers design-time builds, restores, or writes outside the approved scope.

Inventory:

- Direct and inherited `IDisposable` and `IAsyncDisposable` implementations.
- Disposable structs and lease/registration tokens.
- Pattern-disposable types as a separate category; verify analyzer support.
- `SafeHandle` descendants and resource-owning wrappers.
- Raw handles and manual release APIs as a separate, currently unmodeled category.
- Interfaces/abstract bases that carry contracts, separately from instantiable types.

Follow in-repository base types transitively and distinguish nested types and
generic arity. Merge partial declarations by symbol when binding is available;
otherwise preserve declaration rows and qualify any unique-type count.
Retain unresolved external bases,
ambiguous type names, conditional compilation, and unavailable projects as
coverage gaps. Do not label a direct `: IDisposable` text search exhaustive.

For every inventory entry record:

| Field | Evidence |
| --- | --- |
| Identity | Project/assembly, qualified type, arity, declaration locations |
| Classification | Production/test/vendor; direct/inherited/pattern/unknown |
| Lifetime role | Resource owner, borrowed facade, lease, aggregate, marker |
| Resource impact | Native resource, connection, timer, subscription, lock, managed-only, unknown |
| Cleanup | Actual cleanup implementation and owned fields; not merely the method name |
| Usage | Construction/factories, returned values, parameters, member storage, representative callers |
| Confidence | Semantic resolution or bounded syntax/manual tracing; unresolved edges |

## Find high-value producer/consumer pairs

Group calls by resolved member identity, overload, and relevant arguments.
Include properties and awaited `Task<T>`/`ValueTask<T>` results. Keep disposal
of a task distinct from disposal of the resource produced by awaiting it.

Look for an API whose result is:

- Disposed with `using`, `await using`, `Dispose`, or `DisposeAsync` by some callers.
- Ignored, retained in a local, stored, returned, or passed onward by other callers.
- Transferred through a wrapper, callback, task completion, queue, or collection.

For each promising pair, trace both call paths to a terminal owner. Check whether
the API allocates, returns shared state, lends a pooled object, or transfers an
existing object. Freshness is evidence, not a substitute for an ownership contract.

```csharp
using var a = sessions.Get();
var b = sessions.Get();
owner.Attach(b);
```

This is not a leak report until `Get`, `Attach`, and the owner's teardown have
been investigated. Repeated calls may return the same object, and one caller's
existing `using` may be the bug.

Caching does not by itself prove borrowing. A cached release token can represent
one disposal duty for each successful lock acquisition, even when several
acquisitions receive the same object. Conversely, constructing that token may not
acquire anything. Trace acquisition/release state, not just allocation and identity.

Inspect ownership-changing arguments such as `leaveOpen`, `disposeHandler`, and
pool/retention options. Do not generalize one overload or argument combination
into an unconditional contract for every call.

## Rank without inflating certainty

Prioritize resource cost, call frequency, retention duration, and affected
production paths. A cancellation source that owns a timer or registrations is
different evidence from an otherwise unused disposable marker.

Record at least one negative control: a mixed-use pattern that is safe because
of sharing or a verified transfer. Include candidates where `DoNotDispose`
would prevent an inappropriate cleanup, not only missing-dispose candidates.

Separate results into:

1. Evidence-backed missing cleanup or misuse.
2. Correct runtime ownership with missing analyzer-visible contracts.
3. Unresolved ownership requiring an API-owner decision.
4. Analyzer false positive or unsupported transport.
5. Real risk outside v1, such as exceptional-path or lifecycle cleanup.

Deduplicate multi-target findings by rule, source location, and modeled resource;
retain the target frameworks as evidence rather than counting each as a new bug.

## Deliverables

Save a private machine-readable inventory and a code-linked review queue.
State files/projects considered, direct and transitive discovery methods,
classification counts, unresolved bases, and manually traced coverage.
Do not confuse the number of matches with the number of audited lifetimes.

Feed the strongest candidates into
[ownership-contract-review](../ownership-contract-review/SKILL.md).

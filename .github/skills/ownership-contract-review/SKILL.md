---
name: ownership-contract-review
description: >-
  Review disposable ownership boundaries before adding ErrorProne.NET source or
  external annotations. Use to classify ownership-oblivious APIs, borrowing,
  transfers, mixed-cleanup callers, and suspected ownership analyzer false positives.
---

# Review an ownership contract

Read the current [ERP044](../../../docs/Rules/ERP044.md),
[ERP045](../../../docs/Rules/ERP045.md), and
[ERP046](../../../docs/Rules/ERP046.md) contracts before proposing a change.

## Build a lifetime record

For one producer/consumer boundary, document:

- Which instance is created, returned, passed, stored, or shared.
- Who is responsible before the call, after success, and after failure.
- Whether the result aliases an input, a receiver, a cached value, or a fresh value.
- Who releases retained state, including replacement and shutdown paths.
- Whether source, inherited, or external contracts already apply.
- What today's analyzer actually observes versus what manual tracing establishes.

Treat task completion, callbacks, aggregate collections, certificate collections,
and configurable wrappers as boundaries to investigate. Do not add early `using`
to silence a finding if the value must remain live after the method returns.

For a composite result, trace every independently acquired underlying resource.
Disposing a returned wrapper may close a stream but leave its connection owner
unreleased. A local `using` on that connection before returning the wrapper is
not a fix; the composite needs a truthful ownership and teardown design.

## Choose the smallest truthful contract

| Established behavior | Candidate annotation |
| --- | --- |
| Result transfers cleanup responsibility to its recipient | `ReturnsOwnership` on method/property/return |
| Recipient must neither dispose nor transfer a result | `DoNotDispose`, preferably return-targeted for methods |
| Callee acquires responsibility for an argument | `AcquiresOwnership` on that parameter |
| Callee only borrows an argument | `DoNotDispose` on that parameter |
| Ownership varies with arguments or remains uncertain | Keep unknown; document the decision or model limitation |

Do not label all results of a disposable type as owned. Do not label unknown
results borrowed merely to reduce warning volume. Do not annotate generic
collections, task-completion methods, or callback APIs globally based on one
application's lifetime convention.

An array or list is not automatically an ownership contract for its elements.
Trace element cleanup in the actual aggregate owner. Likewise, successful task
publication and eventual consumption are distinct: a task can be abandoned, and
`TrySetResult` can reject a value. Do not model conditional acceptance as an
unconditional acquiring parameter.

If one local aliases a borrowed input on one branch and a newly created copy on
another, dispose only the copy. An unconditional `using` on the merged alias can
turn a real cleanup omission into disposal of somebody else's resource.

Input and result contracts remain independent:

```csharp
[return: DoNotDispose]
Resource Replace(
    [AcquiresOwnership] Resource incoming,
    [DoNotDispose] Resource shared)
{
    StoreOwned(incoming);
    return shared;
}
```

The shared result does not discharge `incoming`; `StoreOwned` must establish
that transfer. A return contract also does not assert that the result aliases a
particular argument.

Ask one concrete decision at a time. Present representative caller code,
the implementation evidence, the proposed owner, and the recommended answer.
A discussion or context-only reply is not approval to apply attributes.

## Source versus external annotations

Use source attributes for owned APIs when changes are approved. The
[annotations generator](../../../src/ErrorProne.NET.Annotations/README.md)
embeds internal types into `RootNamespace` or its explicit override; its public
API usages survive full and reference-assembly emission. No runtime annotation
DLL or public attribute mode is needed.

Use `.ownership.xml` through the consuming project's `AdditionalFiles` when
the API cannot be edited:

```xml
<ownership>
  <member id="M:Example.Factory.Open" assembly="Example.Library" returns="owned" />
  <member id="M:Example.Factory.GetShared" assembly="Example.Library" returns="borrowed" />
  <member id="M:Example.Owner.Take(System.IDisposable)" assembly="Example.Library">
    <parameter name="resource" ownership="owned" />
  </member>
</ownership>
```

Obtain declaration IDs from resolved symbols, such as
`DocumentationCommentId.CreateDeclarationId`, rather than guessing overloaded,
generic, constructor, or explicit-interface syntax. Use the assembly's simple
name and actual parameter names. Verify resolution in the intended compilation:
an absent member can be legal in an annotation pack, so no ERP045 does not prove
that an entry matched. Source contracts take precedence over XML.

The v1 XML schema cannot express arbitrary conditional ownership. Never turn
`leaveOpen: true` or a conditional ownership flag into an unconditional transfer.

## Validate the decision, not just compilation

After authorization, require a positive and a negative example for the boundary:
missing cleanup reports, legitimate cleanup/transfer does not, and borrowed
cleanup reports where applicable. Check library consumers separately from
same-compilation inference. Verify independent incoming and outgoing obligations.

If the issue is in the analyzer, create an independently written minimal
regression in ErrorProne.NET. Reproduce the diagnostic before changing analysis,
then check adjacent ownership cases and the real pilot again.

Keep negative-control scenarios for retained task results, reusable release
tokens, explicit shutdown-callback cleanup, and collection-element transfers.
Known completed-task wrappers transport result identity; unknown publication,
collection arguments and captures make ownership uncertain and silence ERP044.
Distinguish these outcomes: silence at an unknown handoff does not prove
successful transfer, callback execution or eventual cleanup. Contrast each with
local abandonment, discarded completed wrappers, explicit borrowed boundaries
and ordinary receiver use. Failed-transfer cases can remain intentionally
undetected under the low-noise policy; record that trade-off instead of claiming
that a green build establishes safety.

Keep fixes to consumer lifetimes, fixes to analyzer modeling, and intentional
trusted contracts separate in the review record. Never close an uncertain case
as a confirmed leak or a proven-safe transfer.

## Record v1 gaps explicitly

Separate a supported contract that behaves incorrectly from a documented
analysis limit, missing/incorrect consumer metadata, or an integration problem
such as stale compiler output. Keep both noisy correct lifetimes and missed
unsafe lifetimes in the review queue.

For each gap, state the expected lifetime, observed diagnostic or absence,
minimal independently written reproduction, affected real-world pattern,
current documented boundary, and the decision needed before expanding v1.
Characterization tests can record a known limitation without changing policy;
name them accordingly. Passing such a test means the limitation was reproduced,
not that the example's lifetime is safe.

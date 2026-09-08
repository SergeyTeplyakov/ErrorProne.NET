---
name: ownership-adoption
description: >-
  Plan and run a staged adoption of ErrorProne.NET disposable ownership in an
  existing C# repository. Use for local NuGet pilots, disposable-usage audits,
  finding annotation candidates, or evaluating ERP044, ERP045, and ERP046 before
  rollout. Coordinate inventory, contract review, and real-project validation.
---

# Disposable ownership adoption

Adoption is an evidence-gathering exercise, not a request to put `using` around
every disposable value. An analyzer finding is an unfulfilled modeled obligation,
not proof of a runtime leak. A clean build is not a proof of ownership safety.

## Establish the boundary

Read repository instructions and record the checkout, commit, branch, dirty
files, build entry points, compiler version, and existing analyzer configuration.
Find these facts yourself; ask the user only for material decisions:

- Is this inventory-only, a package/configuration pilot, or an approved source fix?
- May local branches be created, and which files/projects may change?
- Should the review include real risks outside the current analyzer's coverage?

For a large repository, agree on a broad inventory plus deeply traced, ranked
candidates. Do not quietly substitute a few interesting files for an inventory.
Independent repositories may be investigated in parallel; keep source-analysis
ownership separate from package/build work to avoid duplicated investigation.

Keep proprietary source, paths, service names, findings, and build logs in the
consumer repository or an explicitly private artifact directory. Reusable skills
and public analyzer regressions must use independently written, generic examples.
Do not publish, commit, push, change global NuGet settings, or run live integration
tests without the corresponding authorization.

## Workflow

1. Read [ERP044](../../../docs/Rules/ERP044.md),
   [ERP045](../../../docs/Rules/ERP045.md), and
   [ERP046](../../../docs/Rules/ERP046.md). These documents are the policy authority.
2. Use [ownership-inventory](../ownership-inventory/SKILL.md) to map disposable
   implementations, producer APIs, consumers, and mixed-cleanup call sites.
3. Use [ownership-pilot](../ownership-pilot/SKILL.md) to publish an immutable local
   preview, establish the unchanged baseline, and exercise representative projects.
4. Use [ownership-contract-review](../ownership-contract-review/SKILL.md) to
   classify findings and agree on individual ownership boundaries.
5. After source changes are authorized, annotate the smallest justified boundary,
   rerun the pilot, and verify both intended diagnostics and negative controls.
6. Generalize demonstrated lessons into the adoption workflow; retain uncertainty
   and untested steps rather than presenting a draft procedure as proven.

## Non-negotiable v1 distinctions

| Distinction | Adoption consequence |
| --- | --- |
| Disposable capability versus instance ownership | A disposable type does not make every result owning. |
| Unknown versus borrowed | Missing annotations do not prohibit disposal. |
| Input versus output ownership | A consumed parameter and a borrowed result are independent contracts. |
| Recognized transfer versus proven final cleanup | Member storage and explicit contracts are trusted boundaries. |
| Source inference versus library contracts | A simple fresh return may be inferred in one compilation but not across a project/assembly boundary. |
| Diagnostic versus defect | Missing knowledge about a task, callback, collection, or wrapper can produce a finding on correct code. |
| No diagnostic versus safety | Exceptional paths, lifecycle teardown, and general escapes remain outside a full proof. |

Do not expand the ownership model or invent type-wide attributes as an adoption
shortcut. Separate necessary analyzer improvements from consumer-code changes.

## Handoff

Deliver the package IDs/version/feed, reproducible pilot commands, baseline and
pilot outcomes, inventory coverage and gaps, and a ranked review queue. Each
candidate needs a producer, representative callers, the likely lifetime owner,
confidence, current analyzer coverage, and the next decision.

Use a concrete question, for example:

```csharp
using var first = provider.Open();
var second = provider.Open();
cache.Remember(second);
```

Determine whether `Remember` owns and eventually releases its argument before
asking whether to add disposal. Present source evidence and a recommended
contract; never infer approval from a context-only reply.

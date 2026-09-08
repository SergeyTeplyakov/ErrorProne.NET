---
name: ownership-pilot
description: >-
  Build and consume a local ErrorProne.NET ownership preview in real C# projects.
  Use for immutable NuGet folder feeds, opt-in analyzer/source-generator wiring,
  isolated MSBuild assets, baseline comparisons, and ERP044/045/046 rollout evidence.
---

# Run a real-project ownership pilot

## Publish a local preview

Record the analyzer commit and exact emitted package versions. Build and pack
`ErrorProne.Net.Annotations` and `ErrorProne.NET.CoreAnalyzers` using the existing
projects and build tools. Do not change dependency versions to hide restore issues.

Use an explicit local folder feed. Never omit the destination in a publishing
command, publish to a remote feed, or add a global package source by default.
For this repository, the analyzer package is produced by the CoreAnalyzers
CodeFixes project, not the implementation project's placeholder package ID.

Account for build-time versioning: inspect the actual `.nupkg` version rather
than assuming a command-line `PackageVersion` override won. Do not overwrite
different package contents under the same ID/version. Record package hashes.
Projects with `GeneratePackageOnBuild` may require an explicit configuration
build before packing; a missing Release DLL is not a NuGet restore problem.

Inspect the package archive for analyzer DLLs and required build-transitive
assets, especially compiler-property forwarding for the generator. There must
be no `lib`/`runtimes` assets from these build-only packages.

## Establish a baseline first

Inspect SDK/compiler versions, central package management, current analyzer
versions, warnings-as-errors, custom output paths, and the repository's supported
build commands. The generator requires Roslyn 4.13 or newer; a project's target
framework and its compiler version are separate facts.

Build the smallest representative real project without the preview. If the
baseline fails because dependencies are missing, restore through the repository's
existing sources and repeat. Distinguish baseline/environment failures from
preview regressions; do not declare an unchanged failure caused by the analyzer.

Never run live integration tests, production startup, or deployment targets as
a shortcut to validation. Use existing targeted, local tests.

## Make rollout opt-in and reversible

Preserve normal defaults. Prefer an explicit pilot property and, for a large
graph, a project selector. Replace the selected project's existing analyzer
reference rather than loading old and new analyzer assemblies together.

Pin both preview package versions and keep:

```xml
PrivateAssets="all"
IncludeAssets="analyzers;build;buildtransitive"
```

Respect the repository's actual package-management mechanism. NuGet central
package management and `Microsoft.Build.CentralPackageVersions` do not have
identical import order or global-reference semantics.

Add the local feed only for the pilot invocation, for example through
`RestoreAdditionalProjectSources`. Retain the established authenticated sources.
Verify NuGet's restored metadata identifies the intended local package.

Isolate preview restore assets, intermediate compilation, generated sources,
binary outputs, package outputs, and diagnostic logs from the normal build.
Separate package selection from artifact isolation: a test project may consume
a preview-built dependency without enabling the preview analyzer itself, but
its copied dependencies and test output still belong to the isolated invocation.
Set early MSBuild path properties before they are consumed; evaluate the final
paths per project and target framework. Do not globally force all referenced
projects to share one assets file or output directory.

Trace later output overrides, not just the pilot's first property assignment.
Evaluate representative library, application, and custom-package-ID shapes and
compare `OutputPath`, `OutDir`, `TargetDir`, and package paths with normal builds.
Limit the supported graph explicitly and fail closed for unsupported project
kinds or layouts. Keep pilot builds out of normal release package staging and
deployment targets; preserve normal output precedence when the pilot is off.

Keep generated files outside source globs. Existing generators, such as
nullability polyfills, may emit additional attributes; count this generator's
output by producer, not every `*Attribute.g.cs` in the directory.

Check `git status` and ignore rules for every required new import. A successful
local build can hide a broken patch when its new `.props` files are ignored.
Expose only the required files; do not unignore arbitrary build output.

## Exercise and classify

For a dirty-source preview, verify the effective package version after the
versioning targets execute, not just at project evaluation. Git-based version
generators can override a command-line `PackageVersion`. Pack into an isolated
staging folder, inspect the archive identity and hashes, then copy to the local
feed without overwriting an existing version. Record the base commit and dirty
source-input/diff hashes; a commit-shaped version alone is insufficient.

Restore and build selected projects using the preview. Capture structured
diagnostics, preferably separate SARIF files per project/target. Preserve
warnings-as-errors and existing analysis; do not add broad suppressions or
disable analyzers to obtain a green result.

Changing the project selector or whole-graph scope changes effective package
references. Restore with that same selection, and inspect the selected project's
actual assets and resolved compiler analyzers. A forced rebuild with stale assets
can succeed without loading the requested preview at all. Fresh SARIF and a
correct package archive on disk do not, by themselves, establish analyzer loading.

A graph may stop at an early ownership diagnostic. Record that failure, then
use a clearly documented project-scoped pilot to inspect other projects without
silently weakening the original graph's policy.

Build a small provider and a real caller when testing a cross-project contract.
Compiling only the provider can confirm annotation generation but cannot prove
that a downstream call receives the intended ownership diagnostic.

For an annotation-driven experiment, record the same source and preview with
the contract off and on, then a correct cleanup/transfer control with it on.
An existing unrelated diagnostic can prevent an otherwise useful comparison
from becoming green. Record that baseline separately; if authorized, temporarily
discharge that independent obligation in every comparison, then restore only
your control edits. Never label an unrelated compiler failure as successful
detection of the intended ownership defect.

Force compilation for these measurements, for example with `--no-incremental`.
Changing a conditional `AdditionalFiles` item set can reuse old successful
compiler output when the newly included file is older than that output.
Where appropriate, expose the contract-mode property through
`CompilerVisibleProperty` so the generated analyzer configuration also changes.
Verify an ordinary incremental off-to-on transition separately. Preserve each
stage's SARIF only after confirming compilation actually ran.

When the user requests a deliberately failing pilot, retain only the opt-in
contract/configuration and the original source defect, not an injected runtime
leak. Record the expected rule, source span, and exit code. Promote a warning
to an error only in that narrowly selected experiment if needed; do not alter
normal warning policy. Restore temporary cleanup controls and confirm both the
expected opt-in failure and the unaffected normal build.

If the user subsequently requests permanent source fixes, retain the corrected
source with the contracts enabled; do not reintroduce the original omission.
Keep historical negative-control evidence separate from the corrected build.
Choose cleanup boundaries from actual ownership: materializers consume readers,
returned streams outlive their factory call, borrowed inputs are not owned copies,
and scheduled work or retained registry aliases may outlive the current method.
Unknown contracts and unfinished asynchronous teardown remain separate questions,
not permission to add immediate disposal or claim complete lifetime safety.

Separate:

- ERP044/ERP046 ownership findings requiring code/contract review.
- ERP045 or EPANN configuration/annotation failures.
- AD0001 and analyzer loading or compiler compatibility failures.
- Other new diagnostics from upgrading the complete analyzer package.
- Baseline, restore, compiler, test-host, or environment failures.

Deduplicate repeated target-framework emissions while retaining target coverage.
Route findings to [ownership-contract-review](../ownership-contract-review/SKILL.md);
do not equate the diagnostic count with a leak count.

ERP044 remains enabled by default but quiets unresolved lifetimes at unknown
argument handoffs and captures. When comparing previews, classify disappearing
diagnostics as proven transport, conservative uncertainty, or an unintended
regression. Include annotated owned-result abandonment and receiver-only use
as positive controls, and explicit borrowed-parameter handoff as a retained
obligation. A lower warning count alone is not increased safety coverage.

Run existing focused tests against the preview-built dependency. Verify a
nonzero matching test count and the actual assembly used. A successful test
command with no restored test SDK or no matching tests is not passing coverage.

Inspect runtime output for accidental ErrorProne DLL dependencies. Verify the
normal build still resolves its original packages and output paths with the
pilot disabled.

## Exit evidence

Save exact package IDs, versions, hashes and source; baseline and pilot commands;
effective project/TFM coverage; diagnostic classifications; test counts; generated
annotation evidence; runtime-output evidence; and remaining blockers.

Do not call a preview merge-ready because packages restored or one project
compiled. Unreviewed diagnostics and unresolved ownership decisions remain open,
even when the build treats them as warnings.

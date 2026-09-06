# Error Prone .NET

ErrorProne.NET is a set of Roslyn-based analyzers that will help you to write correct code. The idea is similar to Google's [error-prone](https://github.com/google/error-prone) but instead of Java, the analyzers are focusing on correctness (and, maybe, performance) of C# programs.

## Installation

Add the following nuget package to you project: https://www.nuget.org/packages/ErrorProne.NET.CoreAnalyzers/

## Rules

## Rules


### Async Analyzers

| Id | Description |
|---|---|
| [EPC14](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC14.md) | ConfigureAwait(false) call is redundant |
| [EPC15](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC15.md) | ConfigureAwait(false) must be used |
| [EPC16](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC16.md) | Awaiting a result of a null-conditional expression will cause NullReferenceException |
| [EPC17](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC17.md) | Avoid async-void delegates |
| [EPC18](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC18.md) | A task instance is implicitly converted to a string |
| [EPC26](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC26.md) | Do not use tasks in using block |
| [EPC27](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC27.md) | Avoid async void methods |
| [EPC31](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC31.md) | Do not return null for Task-like types |
| [EPC32](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC32.md) | TaskCompletionSource should use RunContinuationsAsynchronously |
| [EPC33](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC33.md) | Do not use Thread.Sleep in async methods |
| [EPC35](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC35.md) | Do not block unnecessarily in async methods |
| [EPC36](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC36.md) | Do not use async delegates with Task.Factory.StartNew and TaskCreationOptions.LongRunning |
| [EPC37](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC37.md) | Do not validate arguments in async methods |

### Generic Bugs and Code Smells

| Id | Description |
|---|---|
| [EPC19](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC19.md) | Observe and Dispose a 'CancellationTokenRegistration' to avoid memory leaks |
| [EPC20](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC20.md) | Avoid using default ToString implementation |
| [EPC28](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC28.md) | Do not use ExcludeFromCodeCoverage on partial classes |
| [EPC29](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC29.md) | ExcludeFromCodeCoverageAttribute should provide a message |
| [EPC30](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC30.md) | Method calls itself recursively |
| [ERP041](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/ERP041.md) | EventSource class should be sealed |
| [ERP042](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/ERP042.md) | EventSource implementation is not correct |

### Disposable ownership

The `ErrorProne.Net.Annotations` source-generator package embeds internal
attributes directly in each project's root namespace, without adding a runtime
DLL. Public API contracts remain visible to consuming analyzers through metadata.
See [source-embedded annotations](src/ErrorProne.NET.Annotations/README.md) for
package setup, namespace overrides, and friend-assembly behavior.

Ownership analysis uses attributes and bounded inference: a caller trusts a
transfer, and the consuming method is checked for disposal or further transfer.
Third-party contracts can be supplied as `*.ownership.xml` additional files.
Use `DoNotDispose` for borrowed values, including `[return: DoNotDispose]` on
borrowed results; disposing or transferring them reports ERP046.
Unannotated results are ownership-oblivious by default. A simple, non-overridable
source method directly returning a new disposable can establish ownership;
shared, complex, or external results need an explicit contract to establish a
caller obligation. Unknown does not imply `DoNotDispose`.
The rules intentionally do not attempt a complete borrow checker or proof of
exception safety; see the documented limitations. This first version focuses
on contracts and simple inference. Dedicated diagnostics for unknown ownership
escapes are deferred; missing knowledge is not itself evidence of unsafe code.

| Id | Description |
|---|---|
| [ERP044](docs/Rules/ERP044.md) | Dispose owned resources or transfer their ownership |
| [ERP045](docs/Rules/ERP045.md) | Invalid external ownership annotation |
| [ERP046](docs/Rules/ERP046.md) | Obvious use after disposal/transfer or misuse of explicit borrowing |

### Concurrency

| Id | Description |
|---|---|
| [ERP031](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/ERP031.md) | The API is not thread-safe |

### Error Handling Issues

| Id | Description |
|---|---|
| [EPC11](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC11.md) | Suspicious equality implementation |
| [EPC12](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC12.md) | Suspicious exception handling: only the 'Message' property is observed in the catch block |
| [EPC13](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC13.md) | Suspiciously unobserved result |
| [EPC34](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC34.md) | Method return value marked with MustUseResultAttribute must be used |
| [ERP021](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/ERP021.md) | Incorrect exception propagation |
| [ERP022](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/ERP022.md) | Unobserved exception in a generic exception handler |
| [EPC42](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC42.md) | A member of a data contract is not serializable |

### Performance

| Id | Description |
|---|---|
| [EPC23](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC23.md) | Avoid using Enumerable.Contains on HashSet<T> |
| [EPC24](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC24.md) | A hash table "unfriendly" type is used as the key in a hash table |
| [EPC25](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC25.md) | Avoid using default Equals or HashCode implementation from structs |
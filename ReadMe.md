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
| [EPC20](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC20.md) | Avoid using default ToString implementation |
| [EPC26](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC26.md) | Do not use tasks in using block |
| [EPC27](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC27.md) | Avoid async void methods |
| [EPC31](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC31.md) | Do not return null for Task-like types |
| [EPC32](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC32.md) | TaskCompletionSource should use RunContinuationsAsynchronously |
| [EPC33](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC33.md) | Do not use Thread.Sleep in async methods |
| [EPC35](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC35.md) | Do not block unnecessarily in async methods |

### Generic Bugs and Code Smells

| Id | Description |
|---|---|
| [EPC19](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC19.md) | Observe and Dispose a 'CancellationTokenRegistration' to avoid memory leaks |
| [EPC28](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC28.md) | Do not use ExcludeFromCodeCoverage on partial classes |
| [EPC29](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC29.md) | ExcludeFromCodeCoverageAttribute should provide a message |
| [EPC30](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC30.md) | Method calls itself recursively |
| [ERP041](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/ERP041.md) | EventSource class should be sealed |
| [ERP042](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/ERP042.md) | EventSource implementation is not correct |

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

### Performance

| Id | Description |
|---|---|
| [EPC23](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC23.md) | Avoid using Enumerable.Contains on HashSet<T> |
| [EPC24](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC24.md) | A hash table "unfriendly" type is used as the key in a hash table |
| [EPC25](https://github.com/SergeyTeplyakov/ErrorProne.NET/tree/master/docs/Rules/EPC25.md) | Avoid using default Equals or HashCode implementation from structs |
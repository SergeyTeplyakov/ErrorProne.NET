; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/master/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
EPC11 | ErrorHandling | Warning | SuspiciousEqualsMethodAnalyzer
EPC12 | ErrorHandling | Warning | SuspiciousExceptionHandlingAnalyzer
EPC13 | ErrorHandling | Warning | UnobservedResultAnalyzer
EPC14 | Async | Info | RedundantConfigureAwaitFalseAnalyzer
EPC15 | Async | Warning | ConfigureAwaitRequiredAnalyzer
EPC16 | Async | Warning | NullConditionalOperatorAnalyzer
EPC17 | Async | Warning | AsyncVoidDelegateAnalyzer
EPC18 | Async | Warning | TaskInstanceToStringConversionAnalyzer
EPC19 | CodeSmell | Warning | CancellationTokenRegistrationAnalyzer
EPC20 | Async | Warning | DefaultToStringImplementationUsageAnalyzer
EPC23 | Performance | Warning | HashSetContainsAnalyzer
EPC24 | Performance | Warning | HashTableIncompatibilityAnalyzer
EPC25 | Performance | Warning | DefaultEqualsOrHashCodeUsageAnalyzer
EPC26 | Async | Warning | TaskInUsingBlockAnalyzer
EPC27 | Async | Warning | AsyncVoidMethodAnalyzer
EPC28 | CodeSmell | Warning | ExcludeFromCodeCoverageOnPartialClassAnalyzer
EPC29 | CodeSmell | Warning | ExcludeFromCodeCoverageMessageAnalyzer
EPC30 | CodeSmell | Warning | RecursiveCallAnalyzer
EPC31 | Async | Warning | DoNotReturnNullForTaskLikeAnalyzer
EPC32 | Async | Warning | TaskCompletionSourceRunContinuationsAnalyzer
EPC33 | Async | Warning | DoNotUseThreadSleepAnalyzer
EPC34 | ErrorHandling | Warning | MustUseResultAnalyzer
EPC35 | Async | Warning | DoNotBlockUnnecessarilyInAsyncMethodsAnalyzer
ERP021 | ErrorHandling | Warning | ThrowExAnalyzer
ERP022 | ErrorHandling | Warning | SwallowAllExceptionsAnalyzer
ERP031 | Concurrency | Warning | ConcurrentCollectionAnalyzer
ERP041 | CodeSmell | Warning | EventSourceSealedAnalyzer
ERP042 | CodeSmell | Warning | EventSourceAnalyzer

; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/master/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
EPC11 | ErrorHandling | Warning | SuspiciousEqualsMethodAnalyzer
EPC12 | ErrorHandling | Warning | SuspiciousExceptionHandlingAnalyzer
EPC13 | ErrorHandling | Warning | UnobservedResultAnalyzer
EPC14 | Async | Info | RedundantConfigureAwaitFalseAnalyzer
EPC15 | Async | Info | ConfigureAwaitRequiredAnalyzer
EPC16 | Async | Warning | NullConditionalOperatorAnalyzer
EPC17 | Async | Warning | AsyncVoidDelegateAnalyzer
EPC18 | Async | Warning | TaskInstanceToStringConversionAnalyzer
EPC19 | CodeSmell | Info | CancellationTokenRegistrationAnalyzer
EPC20 | CodeSmell | Warning | DefaultToStringImplementationUsageAnalyzer
EPC23 | Performance | Warning | HashSetContainsAnalyzer
EPC24 | Performance | Warning | HashTableIncompatibilityAnalyzer
EPC25 | Performance | Warning | DefaultEqualsOrHashCodeUsageAnalyzer
EPC26 | Async | Warning | TaskInUsingBlockAnalyzer
EPC27 | Async | Info | AsyncVoidMethodAnalyzer
EPC28 | CodeSmell | Info | ExcludeFromCodeCoverageOnPartialClassAnalyzer
EPC29 | CodeSmell | Info | ExcludeFromCodeCoverageMessageAnalyzer
EPC30 | CodeSmell | Warning | RecursiveCallAnalyzer
EPC31 | Async | Warning | DoNotReturnNullForTaskLikeAnalyzer
EPC32 | Async | Info | TaskCompletionSourceRunContinuationsAnalyzer
EPC33 | Async | Info | DoNotUseThreadSleepAnalyzer
EPC34 | ErrorHandling | Warning | MustUseResultAnalyzer
EPC35 | Async | Info | DoNotBlockUnnecessarilyInAsyncMethodsAnalyzer
EPC36 | Async | Info | DoNotUseAsyncDelegatesForLongRunningTasksAnalyzer
EPC37 | Async | Info | DoNotValidateArgumentsInAsyncMethodsAnalyzer
ERP021 | ErrorHandling | Warning | ThrowExAnalyzer
ERP022 | ErrorHandling | Warning | SwallowAllExceptionsAnalyzer
ERP031 | Concurrency | Warning | ConcurrentCollectionAnalyzer
ERP041 | CodeSmell | Info | EventSourceSealedAnalyzer
ERP042 | CodeSmell | Warning | EventSourceAnalyzer

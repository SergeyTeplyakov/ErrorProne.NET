; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/master/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
EPC11 | CodeSmell | Warning | SuspiciousEqualsMethodAnalyzer
EPC12 | CodeSmell | Warning | SuspiciousExceptionHandlingAnalyzer
EPC13 | CodeSmell | Warning | UnobservedResultAnalyzer
EPC14 | CodeSmell | Info | RemoveConfigureAwaitAnalyzer
EPC15 | CodeSmell | Warning | AddConfigureAwaitAnalyzer
EPC16 | CodeSmell | Warning | NullConditionalOperatorAnalyzer
EPC17 | CodeSmell | Warning | AsyncVoidLambdaAnalyzer
EPC18 | CodeSmell | Warning | TaskInstanceToStringConversionAnalyzer
EPC19 | CodeSmell | Warning | CancellationTokenRegistrationAnalyzer
EPC20 | CodeSmell | Warning | DefaultToStringImplementationUsageAnalyzer
EPC23 | Performance | Warning | HashSetContainsAnalyzer
EPC24 | Performance | Warning | HashTableIncompatibilityAnalyzer
EPC25 | Performance | Warning | DefaultEqualsOrHashCodeUsageAnalyzer
EPC26 | CodeSmell | Warning | TaskInUsingBlockAnalyzer
EPC27 | CodeSmell | Warning | AsyncVoidMethodAnalyzer
EPC28 | CodeSmell | Warning | ExcludeFromCodeCoverageOnPartialClassAnalyzer
EPC29 | CodeSmell | Warning | ExcludeFromCodeCoverageMessageAnalyzer: Warn when ExcludeFromCodeCoverageAttribute is used without a message argument.
EPC30 | CodeSmell | Warning | RecursiveCallAnalyzer: Warns when a method calls itself recursively (conditionally or unconditionally).
EPC31 | CodeSmell | Warning | DoNotReturnNullForTaskLikeAnalyzer
EPC32 | CodeSmell | Warning | TaskCompletionSourceRunContinuationsAnalyzer
EPC33 | CodeSmell | Warning | DoNotUseThreadSleepAnalyzer
ERP021 | CodeSmell | Warning | ThrowExAnalyzer
ERP022 | CodeSmell | Warning | SwallowAllExceptionsAnalyzer
ERP031 | Concurrency | Warning | ConcurrentCollectionAnalyzer
ERP041 | CodeSmell | Warning | EventSourceSealedAnalyzer
ERP042 | CodeSmell | Warning | EventSourceAnalyzer

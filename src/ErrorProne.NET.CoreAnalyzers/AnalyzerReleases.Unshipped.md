; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/master/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
EPC11 | CodeSmell | Warning | SuspiciousEqualsMethodAnalyzer
EPC12 | CodeSmell | Warning | SuspiciousExceptionHandlingAnalyzer
EPC13 | CodeSmell | Warning | UnobservedResultAnalyzer
EPC14 | CodeSmell | Warning | RemoveConfigureAwaitAnalyzer
EPC15 | CodeSmell | Warning | AddConfigureAwaitAnalyzer
EPC16 | CodeSmell | Warning | NullConditionalOperatorAnalyzer
EPC17 | CodeSmell | Warning | AsyncVoidLambdaAnalyzer
EPC18 | CodeSmell | Warning | TaskInstanceToStringConversionAnalyzer
ERP021 | CodeSmell | Warning | ThrowExAnalyzer
ERP022 | CodeSmell | Warning | SwallowAllExceptionsAnalyzer
ERP031 | Concurrency | Warning | ConcurrentCollectionAnalyzer

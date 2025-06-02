﻿; Unshipped analyzer release
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
ERP021 | CodeSmell | Warning | ThrowExAnalyzer
ERP022 | CodeSmell | Warning | SwallowAllExceptionsAnalyzer
ERP031 | Concurrency | Warning | ConcurrentCollectionAnalyzer
ERP041 | CodeSmell | Warning | EventSourceSealedAnalyzer
ERP042 | CodeSmell | Warning | EventSourceAnalyzer

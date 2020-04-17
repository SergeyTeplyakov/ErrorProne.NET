; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/master/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
EPS01 | Performance | Warning | MakeStructReadOnlyAnalyzer
EPS02 | Performance | Warning | NonReadOnlyStructPassedAsInParameterAnalyzer
EPS03 | Performance | Warning | NonReadOnlyStructReturnedByReadOnlyRefAnalyzer
EPS04 | Performance | Warning | NonReadOnlyStructRefReadOnlyLocalAnalyzer
EPS05 | Performance | Warning | UseInModifierForReadOnlyStructAnalyzer
EPS06 | Performance | Warning | HiddenStructCopyAnalyzer
EPS07 | Performance | Warning | HashTableIncompatibilityAnalyzer
EPS08 | Performance | Warning | DefaultEqualsOrHashCodeIsUsedInStructAnalyzer
EPS09 | Usage | Warning | ExplicitInParameterAnalyzer

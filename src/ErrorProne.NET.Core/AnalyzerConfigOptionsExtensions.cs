using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Core
{
    public static class AnalyzerConfigOptionsExtensions
    {
        [SuppressMessage("MicrosoftCodeAnalysisPerformance", "RS1012:Start action has no registered actions.", Justification = "This is not a start action.")]
        public static AnalyzerConfigOptions GetAnalyzerConfigOptions<TLanguageKindEnum>(this CodeBlockStartAnalysisContext<TLanguageKindEnum> context)
            where TLanguageKindEnum : unmanaged, Enum
        {
            return context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.CodeBlock.SyntaxTree);
        }

        public static AnalyzerConfigOptions? TryGetAnalyzerConfigOptions(this SymbolAnalysisContext context)
        {
            if (context.Symbol.Locations is { IsDefaultOrEmpty: false } locations
                && locations[0] is { IsInSource: true } location)
            {
                return context.Options.AnalyzerConfigOptionsProvider.GetOptions(location.SourceTree);
            }
            else
            {
                return null;
            }
        }
    }
}

using System.Collections.Immutable;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.StructAnalyzers
{
    /// <summary>
    /// Emits a diagnostic if a struct can be readonly.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MakeStructReadOnlyAnalyzer : DiagnosticAnalyzer
    {
        public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptors.EPS01;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterSymbolAction(AnalyzeStructDeclaration, SymbolKind.NamedType);
        }

        private void AnalyzeStructDeclaration(SymbolAnalysisContext context)
        {
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
            if (!context.TryGetSemanticModel(out var semanticModel))
            {
                return;
            }

            if (ReadOnlyAnalyzer.StructCanBeReadOnly(namedTypeSymbol, semanticModel))
            {
                var diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
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
        public const string DiagnosticId = DiagnosticIds.MakeStructReadonlyDiagnosticId;

        private const string Title = "A struct can be made readonly";
        private const string MessageFormat = "Struct '{0}' can be made readonly";
        private const string Description = "Readonly structs have a better performance when passed or return by readonly reference.";
        private const string Category = "Performance";
        
        // Using warning for visibility purposes
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

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
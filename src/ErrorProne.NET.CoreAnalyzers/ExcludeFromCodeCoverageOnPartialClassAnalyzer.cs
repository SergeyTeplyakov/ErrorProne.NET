using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.CoreAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ExcludeFromCodeCoverageOnPartialClassAnalyzer : DiagnosticAnalyzer
    {
        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [DiagnosticDescriptors.EPC28];

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        }

        private static void AnalyzeNamedType(SymbolAnalysisContext context)
        {
            var namedType = (INamedTypeSymbol)context.Symbol;
            if (namedType.TypeKind != TypeKind.Class || !namedType.IsPartialDefinition())
            {
                return;
            }

            // Check if any part has [ExcludeFromCodeCoverage]
            foreach (var attribute in namedType.GetAttributes())
            {
                if (attribute.AttributeClass.IsClrType(context.Compilation, typeof(ExcludeFromCodeCoverageAttribute)))
                {
                    var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation();
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.EPC28,
                        location,
                        namedType.Name));
                    // The attribute can't be applied to multiple parts of a partial class, so we can exit early.
                    return;
                }
            }
        }
    }
}

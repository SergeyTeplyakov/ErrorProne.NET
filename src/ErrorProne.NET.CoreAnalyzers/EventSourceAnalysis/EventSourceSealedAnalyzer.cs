using System.Collections.Immutable;
using ErrorProne.NET.EventSourceAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// An analyzer that warns when the EventSource class is not sealed.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class EventSourceSealedAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public static DiagnosticDescriptor Rule => DiagnosticDescriptors.ERP041;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <nodoc />
        public EventSourceSealedAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
        }

        private void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
        {
            if (context.ContainingSymbol is INamedTypeSymbol classType &&
                classType.IsEventSourceClass(context.Compilation))
            {
                // Checking if the class is not sealed or abstract
                if (!(classType.IsSealed || classType.IsAbstract))
                {
                    var location = context.Node.GetLocation();

                    if (context.Node is BaseTypeDeclarationSyntax cds)
                    {
                        location = cds.Identifier.GetLocation();
                    }

                    context.ReportDiagnostic(Diagnostic.Create(Rule, location, classType.Name,
                        "Event source types must be sealed or abstract"));
                }
            }
        }
    }
}
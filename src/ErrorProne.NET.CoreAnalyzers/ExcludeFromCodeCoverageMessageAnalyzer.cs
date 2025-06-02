using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.CoreAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ExcludeFromCodeCoverageMessageAnalyzer : DiagnosticAnalyzer
    {
        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [DiagnosticDescriptors.EPC29];

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
        }

        private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
        {
            var attributeSyntax = (AttributeSyntax)context.Node;
            var semanticModel = context.SemanticModel;
            var attrType = semanticModel.GetTypeInfo(attributeSyntax, context.CancellationToken).Type;
            if (attrType == null || !attrType.IsClrType(context.Compilation, typeof(ExcludeFromCodeCoverageAttribute)))
            {
                return;
            }

            // Only check for Justification if the property exists on the attribute type.
            // For .netstandard2.0 the property is missing, so in this case we won't emit a warning.
            var justificationProperty = attrType.GetMembers("Justification").OfType<IPropertySymbol>().FirstOrDefault();
            if (justificationProperty == null)
            {
                // Property does not exist in this framework, do not warn
                return;
            }

            // If no arguments, or only named arguments and none is Justification.
            if (attributeSyntax.ArgumentList == null ||
                attributeSyntax.ArgumentList.Arguments.Count == 0 ||
                attributeSyntax.ArgumentList.Arguments.All(a => a.NameEquals != null) &&
                !attributeSyntax.ArgumentList.Arguments.Any(a => a.NameEquals != null && a.NameEquals.Name.Identifier.ValueText == "Justification"))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.EPC29,
                    attributeSyntax.GetLocation()));
            }
        }
    }
}

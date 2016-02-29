using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection.Metadata;
using ErrorProne.NET.Common;
using ErrorProne.NET.Core;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.OtherRules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ReadOnlyAttributeAnalyzer : DiagnosticAnalyzer
    {
        private static readonly string Title = "Readonly field was never assigned.";
        private static readonly string Message = "Readonly field '{0} is never assigned to, and will always have its default value '{1}'.";
        private static readonly string Description = "Readonly field was never assigned.";

        private const string Category = "CodeSmell";

        private static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(RuleIds.ReadonlyPropertyWasNeverAssigned, Title, Message, Category,
                DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
        }

        private void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context)
        {
            var fieldDeclaration = (FieldDeclarationSyntax) context.Node;
            var fieldSymbol = context.SemanticModel.GetDeclaredSymbol(fieldDeclaration) as IFieldSymbol;

            Contract.Assert(fieldSymbol != null);

            var semanticModel = context.SemanticModel;
            var syntax = fieldSymbol.ContainingType.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree;
            Contract.Assert(syntax != null);

            var writes = syntax.GetRoot()
                .DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Select(id => new { Symbol = semanticModel.GetSymbolInfo(id.Left).Symbol, Id = id })
                .Where(x => x.Symbol != null && x.Symbol.Equals(fieldSymbol))
                .ToList();

            var defaultValue = fieldSymbol.Type.Defaultvalue();
            
            // This rule will trigger warning even for regular readonly fields,
            // because it seems that IDE will show warning only after the build, but not
            // during editing the file!
            if ((fieldSymbol.IsReadOnly || fieldSymbol.HasReadOnlyAttribute()) && writes.Count == 0)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, fieldDeclaration.GetLocation(), fieldSymbol.Name, defaultValue));
            }
        }
    }
}
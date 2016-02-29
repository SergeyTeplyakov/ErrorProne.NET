using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using ErrorProne.NET.Common;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Rules.OtherRules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class FieldAnalyzer : DiagnosticAnalyzer
    {
        // Field was never used info
        private static readonly string FieldWasNeverUsedTitle = "A private field was never used.";
        private static readonly string FieldWasNeverUsedMessage = "{0} field '{1}' was never used.";
        private static readonly string FieldWasNeverUsedDescription = "A private field was never used.";

        private static readonly DiagnosticDescriptor PrivateFieldWasNeverUsedRule = 
            new DiagnosticDescriptor(RuleIds.PrivateFieldWasNeverUsed, FieldWasNeverUsedTitle, FieldWasNeverUsedMessage, Category,
                DiagnosticSeverity.Error, isEnabledByDefault: true, description: FieldWasNeverUsedDescription);

        // Readonly field was assigned in invalid context
        private static readonly string ReadOnlyFieldWasAssignedOutsideConstructorTitle = "A readonly field assignment in an invalid context.";
        private static readonly string ReadOnlyFieldWasAssignedOutsideConstructorMessage = "A readonly field '{0}' cannot be assigned to (except in a constructor or or a variable initializer).";
        private static readonly string ReadOnlyFieldWasAssignedOutsideConstructorDescription = "A readonly field cannot be assigned in an invalid context.";

        private static readonly DiagnosticDescriptor ReadOnlyFieldWasAssignedOutsideConstructorRule = 
            new DiagnosticDescriptor(
                RuleIds.ReadOnlyFieldWasAssignedOutsideConstructor, 
                ReadOnlyFieldWasAssignedOutsideConstructorTitle, 
                ReadOnlyFieldWasAssignedOutsideConstructorMessage, 
                Category,
                DiagnosticSeverity.Error, isEnabledByDefault: true, description: ReadOnlyFieldWasAssignedOutsideConstructorDescription);

        // Readonly fiedl was never assigned
        private static readonly string ReadOnlyFieldWasNeverAssignedTitle = "Readonly field was never assigned.";
        private static readonly string ReadOnlyFieldWasNeverAssignedMessage = "Readonly field '{0}' is never assigned to, and will always have its default value '{1}'.";
        private static readonly string ReadOnlyFieldWasNeverAssignedDescription = "Readonly field was never assigned.";

        private const string Category = "CodeSmell";

        private static readonly DiagnosticDescriptor ReadOnlyFieldWasNeverAssignedRule =
            new DiagnosticDescriptor(RuleIds.ReadonlyFieldWasNeverAssigned, ReadOnlyFieldWasNeverAssignedTitle, ReadOnlyFieldWasNeverAssignedMessage, Category,
                DiagnosticSeverity.Warning, isEnabledByDefault: true, description: ReadOnlyFieldWasNeverAssignedDescription);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            ReadOnlyFieldWasNeverAssignedRule, ReadOnlyFieldWasAssignedOutsideConstructorRule, PrivateFieldWasNeverUsedRule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);

            context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
        }

        private void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
        {
            var assignment = (AssignmentExpressionSyntax) context.Node;

            if (assignment.Left == null) return;

            var targetSymbol = context.SemanticModel.GetSymbolInfo(assignment.Left).Symbol as IFieldSymbol;
            if (targetSymbol == null) return;

            // Need to analyze only custom readonly fields
            if (targetSymbol.HasReadOnlyAttribute())
            {
                foreach (var parent in assignment.EnumerateParents())
                {
                    if (parent is ConstructorDeclarationSyntax)
                    {
                        // We're in the constructor, everything is fine!
                        return;
                    }

                    if (parent is LambdaExpressionSyntax || parent is MemberDeclarationSyntax)
                    {
                        break;
                    }
                }

                // We can get here only when assignment was declared not inside the constructor!
                context.ReportDiagnostic(
                    Diagnostic.Create(ReadOnlyFieldWasAssignedOutsideConstructorRule, assignment.Left.GetLocation(), targetSymbol.Name));
            }
        }

        private void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context)
        {
            var fieldDeclarations = (FieldDeclarationSyntax) context.Node;
            foreach (var fieldDeclaration in fieldDeclarations.Declaration.Variables)
            {
                // Skipping fields with field-like intializers
                if (fieldDeclaration.Initializer != null) continue;

                var symbol = context.SemanticModel.GetDeclaredSymbol(fieldDeclaration);
                var fieldSymbol = symbol as IFieldSymbol;
                // I've used Contract.Assert here, but it failed from time to time:(
                if (fieldSymbol == null) return;

                // Structs with explicit layout attribute should be ignored!
                if (fieldSymbol.ContainingType.HasAttribute(typeof (StructLayoutAttribute))) continue;

                var semanticModel = context.SemanticModel;

                var allNodes =
                    fieldSymbol.ContainingType.DeclaringSyntaxReferences.SelectMany(
                        s => s.SyntaxTree.GetRoot().DescendantNodes());

                var writes = allNodes.OfType<AssignmentExpressionSyntax>()
                    .Select(id => new { Symbol = semanticModel.GetSymbolInfo(id.Left).Symbol, Syntax = id })
                    .Where(s => s.Symbol != null && s.Symbol.Equals(fieldSymbol))
                    .Select(s => s.Syntax)
                    .ToList();

                var writesViaOutputParams = allNodes
                    .OfType<ArgumentSyntax>()
                    .Where(a => a.RefOrOutKeyword != default(SyntaxToken))
                    .Select(id => new {Symbol = semanticModel.GetSymbolInfo(id.Expression).Symbol, Id = id})
                    .Where(x => x.Symbol != null && x.Symbol.Equals(fieldSymbol))
                    .ToList();

                var defaultValue = fieldSymbol.Type.Defaultvalue();

                // This rule will trigger warning even for regular readonly fields,
                // because it seems that IDE will show warning only after the build, but not
                // during editing the file!
                if ((fieldSymbol.IsReadOnly || fieldSymbol.HasReadOnlyAttribute()) && (writes.Count + writesViaOutputParams.Count) == 0)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(ReadOnlyFieldWasNeverAssignedRule, fieldDeclaration.GetLocation(), fieldSymbol.Name, defaultValue));
                }
            }
        }
    }
}
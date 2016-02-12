using System.Collections;
using System.Collections.Immutable;
using ErrorProne.NET.Common;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.SideEffectRules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AssignmentFreeObjectConstructionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = RuleIds.AssignmentFreeObjectContructionId;

        private static readonly string Title = "Assignment-free object construction.";
        private static readonly string Message = "Assignment-free object construction.";
        private static readonly string Description = "Newly created object was not stored in any form!";

        // This is just a warning! Maybe an exception via attributes or something should be added!
        // TODO: add attribute that will suppress this warning!
        private const string Category = "CodeSmell";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, Message, Category,
            DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        }

        private void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
        {
            var objectCreation = (ObjectCreationExpressionSyntax) context.Node;

            var symbol = context.SemanticModel.GetSymbolInfo(objectCreation.Type);

            if (objectCreation.Parent is ExpressionStatementSyntax && !symbol.Symbol.IsExceptionType(context.SemanticModel))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation()));
            }
        }
    }
}
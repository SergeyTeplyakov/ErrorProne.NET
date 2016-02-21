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
    public sealed class AssignmentFreeImmutableObjectConstructionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = RuleIds.AssignmentFreeImmutableObjectContructionId;

        private static readonly string Title = "Assignment-free immutable object construction.";
        private static readonly string Message = "Assignment-free immutable object construction.";
        private static readonly string Description = "Newly created immutable object was not stored in any form!";

        // This is just a warning! Maybe an exception via attributes or something should be added!
        // TODO: add attribute that will suppress this warning!
        private const string Category = "CodeSmell";

        // Disabing this rule, because it leads to tons of false positives
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

            if (objectCreation.Parent is ExpressionStatementSyntax && 
                !symbol.Symbol.IsExceptionType(context.SemanticModel)) //&&
                //symbol.Symbol.IsImmutable(context.SemanticModel))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation()));
            }
        }
    }
}
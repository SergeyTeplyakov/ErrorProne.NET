using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Common;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.SideEffectRules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class SideEffectFreeExceptionConstructionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = RuleIds.SideEffectFreeExceptionContructionId;

        private static readonly string Title = "Side effect free exception creation.";
        private static readonly string Message = "Side effect free exception creation.";
        private static readonly string Description = "Newly created exceptions should be thrown or stored in variables";

        private const string Category = "Bugs";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, Message, Category,
            DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        }

        private void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
        {
            var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

            var symbol = context.SemanticModel.GetSymbolInfo(objectCreation.Type);

            if (objectCreation.Parent is ExpressionStatementSyntax && symbol.Symbol.IsExceptionType(context.SemanticModel))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation()));
            }
        }
    }
}
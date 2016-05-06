using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using ErrorProne.NET.Common;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Rules.OtherRules
{
    /// <summary>
    /// Analyzer that warns when <code>string.Clone()</code> method is invoked.
    /// </summary>
    /// <remarks>
    /// This analyzer could be generalized Clone method invocations on any immutable type.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class StringCloneMethodAnalyzer : DiagnosticAnalyzer
    {
        private static readonly string Title = "string.Clone() method was used.";
        private static readonly string Message = "string.Clone() method was used.";
        private static readonly string Description = "Strings are immutable and cloning of them considered useless.";

        private const string Category = "CodeSmell";

        private static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(RuleIds.StringCloneMethodWasUsed, Title, Message, Category,
                DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeMethodInvocation, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeMethodInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocationExpression = (InvocationExpressionSyntax) context.Node;
            var symbol = context.SemanticModel.GetSymbolInfo(invocationExpression).Symbol as IMethodSymbol;
            if (symbol == null)
            {
                return;
            }

            var stringType = context.SemanticModel.GetClrType(typeof(string));
            if (symbol.ReceiverType.Equals(stringType) && symbol.Name == "Clone")
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, invocationExpression.GetLocation()));
            }
        }
    }
}
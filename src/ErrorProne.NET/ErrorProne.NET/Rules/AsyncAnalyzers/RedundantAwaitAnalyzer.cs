using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Common;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Rules.AsyncAnalyzers
{
    /// <summary>
    /// Analyzer that warns on redundant await statements.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RedundantAwaitAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = RuleIds.RedundantAwaitRule;

        internal const string Title = "Await is redundant";
        internal const string MessageFormat = "Async/Await is redundant because all exit points are awaitable.";
        internal const string Category = "CodeSmell";

        public static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeMethodBody, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethodBody(SyntaxNodeAnalysisContext context)
        {
            var method = (MethodDeclarationSyntax)context.Node;

            if (method.Body.Statements.Count == 0)
            {
                // if there is no expressions in the method, nothing to do.
                return;
            }

            if (!method.IsAsync(context.SemanticModel))
            {
                // Analysis is applicable only for async method that has await statements.
                return;
            }

            int awaitExpressions = AwaitExpressionsCount(method);
            if (awaitExpressions == 0)
            {
                // Not interested if there is no await expressions in the metehod
                return;
            }

            // Now we need to find all return statements in the method
            // and if all of them are await expression than we need to warn.
            var controlFlow = context.SemanticModel.AnalyzeControlFlow(
                method.Body.Statements.First(),
                method.Body.Statements.Last());

            var returns = controlFlow.ExitPoints.OfType<ReturnStatementSyntax>().ToList();

            // This could be greater than number of returns when one return statement
            // has ternary operator.
            var awaitsInReturn = returns.Select(n => NumberOfAwaitExpressions(n.Expression)).Sum();

            // Need to calculate number of 'awaitable' returns once again to check
            // that all of them are awaitable.
            var awaitableReturns = returns.Count(n => NumberOfAwaitExpressions(n.Expression) > 0);

            if (awaitExpressions == awaitsInReturn && awaitableReturns == returns.Count)
            {
                // All returns has await expression in it
                // and this is all awaits in the method body.
                context.ReportDiagnostic(Diagnostic.Create(Rule, method.Identifier.GetLocation()));
            }
        }

        private int NumberOfAwaitExpressions(ExpressionSyntax expression)
        {
            if (expression is AwaitExpressionSyntax)
            {
                return 1;
            }

            // Special case:
            // return x > 0 ? await z : await y;
            var conditional = expression as ConditionalExpressionSyntax;
            if (conditional == null)
            {
                return 0;
            }

            return NumberOfAwaitExpressions(conditional.WhenTrue) + NumberOfAwaitExpressions(conditional.WhenFalse);
        }

        private int AwaitExpressionsCount(MethodDeclarationSyntax method)
        {
            return method.DescendantNodes().OfType<AwaitExpressionSyntax>().Count();
        }
    }
}

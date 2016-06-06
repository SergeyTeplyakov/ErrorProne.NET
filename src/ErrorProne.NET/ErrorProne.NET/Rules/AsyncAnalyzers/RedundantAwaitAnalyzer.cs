using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
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
            // Need to analyze methods, anonymous delegates and lambda-expressions
            context.RegisterSyntaxNodeAction(AnalyzeMethodBody, SyntaxKind.MethodDeclaration);

            context.RegisterSyntaxNodeAction(AnalyzeLambdaExpression, SyntaxKind.ParenthesizedLambdaExpression);
            context.RegisterSyntaxNodeAction(AnalyzeLambdaExpression, SyntaxKind.AnonymousMethodExpression);
        }

        private void AnalyzeLambdaExpression(SyntaxNodeAnalysisContext context)
        {
            var anonymousMethod = (AnonymousFunctionExpressionSyntax) context.Node;

            if (anonymousMethod.AsyncKeyword.RawKind == 0)
            {
                return;
            }

            // Anonymous methods could have blocks or just expression as a body

            var block = anonymousMethod.Body as BlockSyntax;
            if (block?.Statements.Count == 0)
            {
                return;
            }

            // Need to specify method.Body, because otherwise entire content of the anonymous method
            // would be ignored because of the predicate: method.DescendantNodes(n => !(n is AnonymousFunctionExpressionSyntax))
            AnalyzeStatements(context, anonymousMethod.Body, anonymousMethod.AsyncKeyword.GetLocation());
        }

        private void AnalyzeMethodBody(SyntaxNodeAnalysisContext context)
        {
            var method = (MethodDeclarationSyntax)context.Node;

            // method.Body could be null for expression body
            if (method.Body?.Statements.Count == 0)
            {
                // if there is no expressions in the method, nothing to do.
                return;
            }

            if (!method.IsAsync(context.SemanticModel))
            {
                // Analysis is applicable only for async method that has await statements.
                return;
            }

            AnalyzeStatements(context, (method.ExpressionBody?.Expression as SyntaxNode) ?? method, method.Identifier.GetLocation());
        }

        private void AnalyzeStatements(SyntaxNodeAnalysisContext context, SyntaxNode methodBody, Location location)
        {
            // One special case: method body could be an expression and if this expression is await expression
            // then we're done.
            var awaitableMethod = methodBody as AwaitExpressionSyntax;
            if (awaitableMethod != null && IsTaskOfT(awaitableMethod, context.SemanticModel))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, location));
                return;
            }

            // More complex case: memthod (named or anonymous) is a full-featured method. 
            // Need to check it more thoroughly.

            // Need to exclude lambda expression and anonymous delegates from the analysis, because
            // they could have any number of awaits inside.

            var relevantDescendantNodes =
                methodBody.DescendantNodes(n => !(n is AnonymousFunctionExpressionSyntax))
                .ToList();

            var awaitExpressions = relevantDescendantNodes.OfType<AwaitExpressionSyntax>().ToList();
            if (awaitExpressions.Count == 0 || awaitExpressions.Any(a => !IsTaskOfT(a, context.SemanticModel)))
            {
                // Not interested if there is no await expressions in the method,
                // or some of them are not task-based.
                return;
            }

            // Don't need to use control flow to get all return statements
            // because some return statements could be unreachable.
            var returns = relevantDescendantNodes.OfType<ReturnStatementSyntax>().Select(s => s.Expression).ToList();

            // This could be greater than number of returns when one return statement
            // has ternary operator.
            var awaitsInReturn = returns.Select(n => NumberOfAwaitExpressions(n)).Sum();

            // Need to calculate number of 'awaitable' returns once again to check
            // that all of them are awaitable.
            var awaitableReturns = returns.Count(n => NumberOfAwaitExpressions(n) > 0);

            if (awaitExpressions.Count == awaitsInReturn && awaitableReturns == returns.Count)
            {
                // All returns has await expression in it
                // and this is all awaits in the method body.
                context.ReportDiagnostic(Diagnostic.Create(Rule, location));
            }
        }

        private bool IsTaskOfT(AwaitExpressionSyntax awaitExpression, SemanticModel semanticModel)
        {
            var type = semanticModel.GetTypeInfo(awaitExpression.Expression);

            return type.Type?.UnwrapGenericIfNeeded()?.Equals(
                semanticModel.GetClrType(typeof(Task<>))) == true;
        }

        private int NumberOfAwaitExpressions(ExpressionSyntax expression)
        {
            expression = UnwrapParenthesizedExpression(expression);

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

        private ExpressionSyntax UnwrapParenthesizedExpression(ExpressionSyntax expression)
        {
            var parens = expression as ParenthesizedExpressionSyntax;
            if (parens == null)
            {
                return expression;
            }

            return UnwrapParenthesizedExpression(parens.Expression);
        }
    }
}

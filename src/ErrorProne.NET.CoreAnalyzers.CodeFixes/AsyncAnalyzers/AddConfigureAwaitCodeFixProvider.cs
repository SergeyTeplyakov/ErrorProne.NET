using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.AsyncAnalyzers
{
    /// <summary>
    /// A fixer for <see cref="AddConfigureAwaitAnalyzer"/>.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddConfigureAwaitAnalyzer)), Shared]
    public class AddConfigureAwaitCodeFixProvider : CodeFixProvider
    {
        public const string RemoveConfigureAwaitTitle = "Add ConfigureAwait(false).";

        /// <inheritdoc />
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(AddConfigureAwaitAnalyzer.DiagnosticId);

        /// <inheritdoc />
        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            foreach (var diagnostic in context.Diagnostics)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: RemoveConfigureAwaitTitle,
                        createChangedDocument: c => AddConfigureAwait(context.Document, diagnostic.Location, context.CancellationToken),
                        equivalenceKey: RemoveConfigureAwaitTitle),
                    diagnostic);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        private async Task<Document> AddConfigureAwait(Document document, Location location, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);

            var awaitExpression = root.FindToken(location.SourceSpan.Start).Parent.AncestorsAndSelf()
                .OfType<AwaitExpressionSyntax>().First();

            var newExpr = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    awaitExpression.Expression,
                    SyntaxFactory.IdentifierName("ConfigureAwait")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression))))
            );

            var newAwaitExpression = awaitExpression.WithExpression(newExpr);
            var newRoot = root.ReplaceNode(awaitExpression, newAwaitExpression);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
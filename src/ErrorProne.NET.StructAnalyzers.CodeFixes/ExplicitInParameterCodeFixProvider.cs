using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorProne.NET.StructAnalyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExplicitInParameterCodeFixProvider)), Shared]
    public class ExplicitInParameterCodeFixProvider : CodeFixProvider
    {
        public const string Title = "Add 'in' keyword";

        /// <inheritdoc />
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(ExplicitInParameterAnalyzer.DiagnosticId);

        /// <inheritdoc />
        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        /// <inheritdoc />
        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            foreach (var diagnostic in context.Diagnostics)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        Title,
                        cancellationToken => AddInKeywordAsync(context.Document, diagnostic.Location, cancellationToken),
                        nameof(ExplicitInParameterCodeFixProvider)),
                    diagnostic);
            }

            return Task.CompletedTask;
        }

        private static async Task<Document> AddInKeywordAsync(Document document, Location location, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var argument = root?.FindNode(location.SourceSpan, getInnermostNodeForTie: true)?.FirstAncestorOrSelf<ArgumentSyntax>();
            if (argument is null)
            {
                return document;
            }

            var newArgument = argument.WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.InKeyword));
            if (newArgument.Expression is ConditionalExpressionSyntax conditionalExpression)
            {
                newArgument = newArgument.ReplaceNodes(
                    new[] { conditionalExpression.WhenTrue, conditionalExpression.WhenFalse },
                    (originalNode, rewrittenNode) => SyntaxFactory.RefExpression(rewrittenNode));
            }

            return document.ReplaceSyntaxRoot(root.ReplaceNode(argument, newArgument));
        }
    }
}

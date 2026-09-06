using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.EventSourceAnalysis
{
    /// <summary>
    /// A fixer for an empty <c>Message</c> on an <c>[Event]</c> attribute reported by <see cref="EmptyEventMessageAnalyzer"/>.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EmptyEventMessageCodeFixProvider)), Shared]
    public class EmptyEventMessageCodeFixProvider : CodeFixProvider
    {
        public const string RemoveMessageTitle = "Remove Message argument";
        public const string UseSingleSpaceTitle = "Use a single space";

        /// <inheritdoc />
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(EmptyEventMessageAnalyzer.Rule.Id);

        /// <inheritdoc />
        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        /// <inheritdoc />
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return;
            }

            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (semanticModel is null)
            {
                return;
            }

            foreach (var diagnostic in context.Diagnostics)
            {
                var messageArg = root.FindNode(diagnostic.Location.SourceSpan)
                    .FirstAncestorOrSelf<AttributeArgumentSyntax>();

                if (!IsEmptyMessageArgument(messageArg, semanticModel, context.CancellationToken))
                {
                    continue;
                }

                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: RemoveMessageTitle,
                        createChangedDocument: c => RemoveMessageArgumentAsync(context.Document, diagnostic.Location, c),
                        equivalenceKey: RemoveMessageTitle),
                    diagnostic);

                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: UseSingleSpaceTitle,
                        createChangedDocument: c => UseSingleSpaceAsync(context.Document, diagnostic.Location, c),
                        equivalenceKey: UseSingleSpaceTitle),
                    diagnostic);
            }
        }

        private static bool IsEmptyMessageArgument(
            AttributeArgumentSyntax? argument, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            if (argument?.NameEquals?.Name.Identifier.ValueText != "Message")
            {
                return false;
            }

            var constant = semanticModel.GetConstantValue(argument.Expression, cancellationToken);
            return constant.HasValue && constant.Value is string message && message.Length == 0;
        }

        private static async Task<Document> RemoveMessageArgumentAsync(Document document, Location location, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return document;
            }

            var messageArg = root.FindNode(location.SourceSpan).FirstAncestorOrSelf<AttributeArgumentSyntax>();
            if (messageArg is null)
            {
                return document;
            }

            // RemoveNode handles the associated comma separator so we don't leave a dangling/leading comma.
            var newRoot = root.RemoveNode(messageArg, SyntaxRemoveOptions.KeepNoTrivia);
            return newRoot is null ? document : document.WithSyntaxRoot(newRoot);
        }

        private static async Task<Document> UseSingleSpaceAsync(Document document, Location location, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return document;
            }

            var messageArg = root.FindNode(location.SourceSpan).FirstAncestorOrSelf<AttributeArgumentSyntax>();
            if (messageArg is null)
            {
                return document;
            }

            var newLiteral = SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(" "))
                .WithTriviaFrom(messageArg.Expression);

            var newRoot = root.ReplaceNode(messageArg.Expression, newLiteral);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}

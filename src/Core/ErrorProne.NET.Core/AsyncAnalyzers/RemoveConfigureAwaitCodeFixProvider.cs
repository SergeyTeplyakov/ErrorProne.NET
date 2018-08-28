using System;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace ErrorProne.NET.Core.AsyncAnalyzers
{
    /// <summary>
    /// A fixer for <see cref="RemoveConfigureAwaitAnalyzer"/>.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveConfigureAwaitAnalyzer)), Shared]
    public class RemoveConfigureAwaitCodeFixProvider : CodeFixProvider
    {
        public const string RemoveConfigureAwaitTitle = "Remove redundant ConfigureAwait(false).";

        /// <inheritdoc />
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RemoveConfigureAwaitAnalyzer.DiagnosticId);

        /// <inheritdoc />
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.FirstOrDefault();

            if (diagnostic == null)
            {
                // Not sure why, but it seems this is possible in the batch mode.
                return;
            }

            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var identifier = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf()
                .OfType<IdentifierNameSyntax>().First();

            Debug.Assert(identifier.GetText().ToString() == "ConfigureAwait");

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: RemoveConfigureAwaitTitle,
                    createChangedDocument: c => RemoveMethodCall(context.Document, identifier, context.CancellationToken),
                    equivalenceKey: RemoveConfigureAwaitTitle),
                diagnostic);
        }

        /// <inheritdoc />
        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        private async Task<Document> RemoveMethodCall(Document document, IdentifierNameSyntax identifier, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var awaitExpression = identifier.AncestorsAndSelf().OfType<AwaitExpressionSyntax>().First();
            var newExpression =
                awaitExpression
                    .Expression.Cast<InvocationExpressionSyntax>() // await fooBar.ConfigureAwait(false);
                    .Expression.Cast<MemberAccessExpressionSyntax>()
                    .Expression; // fooBar
            // Need to remove a potential trailing new line.
            newExpression = newExpression.WithTrailingTrivia();
            var newAwaitExpression = awaitExpression.WithExpression(newExpression);
            var newRoot = root.ReplaceNode(awaitExpression, newAwaitExpression);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
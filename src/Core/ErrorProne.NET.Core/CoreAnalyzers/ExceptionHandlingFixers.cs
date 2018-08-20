using System;
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

namespace ErrorProne.NET.Core.CoreAnalyzers
{
    /// <summary>
    /// A fixer for <see cref="SuspiciousExceptionHandlingAnalyzer"/>.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SuspiciousExceptionHandlingAnalyzer)), Shared]
    public class ExceptionHandlingFixers : CodeFixProvider
    {
        public const string Title = "Observe the whole exception instance.";

        /// <inheritdoc />
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(SuspiciousExceptionHandlingAnalyzer.DiagnosticIdWithoutSuggestion);

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

            // Find the type declaration identified by the diagnostic.
            var identifier = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<IdentifierNameSyntax>().FirstOrDefault();
            if (identifier != null)
            {
                // Register a code action that will invoke the fix.
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: Title,
                        createChangedDocument: c => UseException(context.Document, identifier, c),
                        equivalenceKey: Title),
                    diagnostic);
            }
        }

        /// <inheritdoc />
        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        private async Task<Document> UseException(Document document, IdentifierNameSyntax identifier, CancellationToken cancellationToken)
        {
            if (identifier.Parent is MemberAccessExpressionSyntax mae)
            {
                var root = await document.GetSyntaxRootAsync(cancellationToken);
                var newRoot = root.ReplaceNode(mae, mae.Expression);
                return document.WithSyntaxRoot(newRoot);
            }

            return document;
        }
    }
}
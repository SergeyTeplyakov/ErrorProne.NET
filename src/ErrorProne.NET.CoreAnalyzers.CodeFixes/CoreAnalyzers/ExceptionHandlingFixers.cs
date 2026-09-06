using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// A fixer for <see cref="SuspiciousExceptionHandlingAnalyzer"/>.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SuspiciousExceptionHandlingAnalyzer)), Shared]
    public class ExceptionHandlingFixers : CodeFixProvider
    {
        public const string Title = "Observe the whole exception instance.";

        /// <inheritdoc />
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(SuspiciousExceptionHandlingAnalyzer.DiagnosticId);

        /// <inheritdoc />
        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            foreach (var diagnostic in context.Diagnostics)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: Title,
                        createChangedDocument: c => UseExceptionAsync(context.Document, diagnostic.Location, c),
                        equivalenceKey: Title),
                    diagnostic);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        private static async Task<Document> UseExceptionAsync(Document document, Location location, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            // Find the type declaration identified by the diagnostic.
            var identifier = root
                ?.FindToken(location.SourceSpan.Start).Parent
                ?.AncestorsAndSelf()
                .OfType<IdentifierNameSyntax>().FirstOrDefault();
            if (root is not null && identifier is { Parent: MemberAccessExpressionSyntax mae })
            {
                var newRoot = root.ReplaceNode(mae, mae.Expression);
                return document.WithSyntaxRoot(newRoot);
            }

            return document;
        }
    }
}
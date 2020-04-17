using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

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
            var diagnostic = context.Diagnostics.FirstOrDefault();

            if (diagnostic == null)
            {
                // Not sure why, but it seems this is possible in the batch mode.
                return Task.CompletedTask;
            }

            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: c => UseException(context.Document, diagnosticSpan, c),
                    equivalenceKey: Title),
                diagnostic);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        private async Task<Document> UseException(Document document, TextSpan diagnosticSpan, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            // Find the type declaration identified by the diagnostic.
            var identifier = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<IdentifierNameSyntax>().FirstOrDefault();
            if (identifier is { Parent: MemberAccessExpressionSyntax mae })
            {
                var newRoot = root.ReplaceNode(mae, mae.Expression);
                return document.WithSyntaxRoot(newRoot);
            }

            return document;
        }
    }
}
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

namespace ErrorProne.NET.Structs
{
    /// <summary>
    /// A fixer for <see cref="MakeStructReadOnlyAnalyzer"/>.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseInModifierForReadOnlyStructCodeFixProvider)), Shared]
    public class UseInModifierForReadOnlyStructCodeFixProvider : CodeFixProvider
    {
        public const string Title = "Pass readonly struct with 'in'-modifier";

        /// <inheritdoc />
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<ParameterSyntax>().First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: c => AddInModifier(context.Document, declaration, c),
                    equivalenceKey: Title),
                diagnostic);
        }

        /// <inheritdoc />
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(UseInModifierForReadOnlyStructAnalyzer.DiagnosticId);

        /// <inheritdoc />
        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        private async Task<Document> AddInModifier(Document document, ParameterSyntax paramSyntax, CancellationToken cancellationToken)
        {
            SyntaxTriviaList trivia = paramSyntax.GetLeadingTrivia(); ;

            var newType = paramSyntax
                .WithModifiers(paramSyntax.Modifiers.Insert(0, SyntaxFactory.Token(SyntaxKind.InKeyword)))
                .WithLeadingTrivia(trivia);

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            return document.WithSyntaxRoot(root.ReplaceNode(paramSyntax, newType));
        }
    }
}
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

namespace ErrorProne.NET.StructAnalyzers
{
    /// <summary>
    /// A fixer for <see cref="MakeStructReadOnlyAnalyzer"/>.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MakeStructReadOnlyCodeFixProvider)), Shared]
    public class MakeStructReadOnlyCodeFixProvider : CodeFixProvider
    {
        public const string Title = "Make struct readonly";

        /// <inheritdoc />
        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            foreach (var diagnostic in context.Diagnostics)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: Title,
                        createChangedDocument: c => MakeReadOnlyAsync(context.Document, diagnostic.Location, c),
                        equivalenceKey: Title),
                    diagnostic);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(MakeStructReadOnlyAnalyzer.DiagnosticId);

        /// <inheritdoc />
        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        private async Task<Document> MakeReadOnlyAsync(Document document, Location location, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            // Find the type declaration identified by the diagnostic.
            var typeDecl = root.FindToken(location.SourceSpan.Start).Parent.AncestorsAndSelf().OfType<StructDeclarationSyntax>().FirstOrDefault();
            if (typeDecl is null)
            {
                return document;
            }

            var readonlyToken = SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword);
            SyntaxTokenList modifiers;
            int partialIndex = typeDecl.Modifiers.IndexOf(SyntaxKind.PartialKeyword);
            if (partialIndex != -1)
            {
                modifiers = typeDecl.Modifiers.Insert(partialIndex, readonlyToken);
            }
            else
            {
                modifiers = typeDecl.Modifiers.Add(readonlyToken);
            }

            var newType = typeDecl.WithModifiers(modifiers);

            return document.WithSyntaxRoot(root.ReplaceNode(typeDecl, newType));
        }
    }
}
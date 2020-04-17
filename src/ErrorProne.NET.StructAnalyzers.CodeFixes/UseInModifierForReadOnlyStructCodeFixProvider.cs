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
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;

namespace ErrorProne.NET.StructAnalyzers
{
    /// <summary>
    /// A fixer for <see cref="MakeStructReadOnlyAnalyzer"/>.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseInModifierForReadOnlyStructCodeFixProvider)), Shared]
    public class UseInModifierForReadOnlyStructCodeFixProvider : CodeFixProvider
    {
        public const string Title = "Pass readonly struct with 'in'-modifier";

        /// <inheritdoc />
        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: c => AddInModifier(context.Document, diagnosticSpan, c),
                    equivalenceKey: Title),
                diagnostic);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(UseInModifierForReadOnlyStructAnalyzer.DiagnosticId);

        /// <inheritdoc />
        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        private async Task<bool> ParameterIsUsedInNonInFriendlyManner(ParameterSyntax parameter, Document document, CancellationToken token)
        {
            if (!document.SupportsSemanticModel)
            {
                // Don't have a semantic model. Not sure what to do in this case, so avoid showing the code fix.
                return true;
            }

            var semanticModel = await document.GetSemanticModelAsync(token);
            var paramSymbol = semanticModel.GetDeclaredSymbol(parameter);
            var references = await SymbolFinder.FindReferencesAsync(paramSymbol, document.Project.Solution, token).ConfigureAwait(false);

            var syntaxRoot = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);

            var method = parameter.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (method == null)
            {
                // Don't have a method (could be an indexer). This is not yet supported, so avoid showing the code fix.
                return true;
            }

            foreach (var reference in references)
            {
                var location = reference.Locations.FirstOrDefault();
                if (location.Location != null)
                {
                    // The reference could be easily outside of the current syntax tree.
                    // For instance, it may be in a partial class?
                    if (!syntaxRoot.FullSpan.Contains(location.Location.SourceSpan))
                    {
                        continue;
                    }

                    var node = syntaxRoot.FindNode(location.Location.SourceSpan);

                    if (node.Parent is ArgumentSyntax arg &&
                        (arg.RefKindKeyword.Kind() == SyntaxKind.OutKeyword ||
                         arg.RefKindKeyword.Kind() == SyntaxKind.RefKeyword))
                    {
                        // Parameter used as out/ref argument and can not be changed to 'in'.
                        return true;
                    }

                    foreach (var parent in node.Ancestors())
                    {
                        if (parent.Equals(method))
                        {
                            break;
                        }

                        var kind = parent.Kind();
                        if (kind == SyntaxKind.ParenthesizedLambdaExpression ||
                            kind == SyntaxKind.SimpleLambdaExpression ||
                            kind == SyntaxKind.AnonymousMethodExpression)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private async Task<Document> AddInModifier(Document document, TextSpan diagnosticSpan, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            // Find the type declaration identified by the diagnostic.
            var paramSyntax = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<ParameterSyntax>().FirstOrDefault();
            if (paramSyntax is null || await ParameterIsUsedInNonInFriendlyManner(paramSyntax, document, cancellationToken).ConfigureAwait(false))
            {
                // It is possible for some weird cases to not have 'ParameterSyntax'. See 'WarnIfParameterIsReadOnly' in UseInModifierAnalyzer.
                return document;
            }

            SyntaxTriviaList trivia = paramSyntax.GetLeadingTrivia(); ;

            var newType = paramSyntax
                .WithModifiers(paramSyntax.Modifiers.Insert(0, SyntaxFactory.Token(SyntaxKind.InKeyword)))
                .WithLeadingTrivia(trivia);

            return document.WithSyntaxRoot(root.ReplaceNode(paramSyntax, newType));
        }
    }
}
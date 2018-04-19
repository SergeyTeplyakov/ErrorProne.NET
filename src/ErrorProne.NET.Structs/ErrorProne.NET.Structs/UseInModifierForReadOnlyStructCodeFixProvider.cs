using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

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

            if (!await ParameterIsUsedInNonInFriendlyManner(declaration, context.Document, context.CancellationToken).ConfigureAwait(false))
            {
                // Register a code action that will invoke the fix.
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: Title,
                        createChangedDocument: c => AddInModifier(context.Document, declaration, c),
                        equivalenceKey: Title),
                    diagnostic);
            }
        }

        /// <inheritdoc />
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(UseInModifierForReadOnlyStructAnalyzer.DiagnosticId);

        /// <inheritdoc />
        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        private async Task<bool> ParameterIsUsedInNonInFriendlyManner(ParameterSyntax parameter, Document document, CancellationToken token)
        {
            if (!document.SupportsSemanticModel)
            {
                // Don't have a semantic model. Not sure what to do in this case.
                return false;
            }

            var semanticModel = await document.GetSemanticModelAsync(token);
            var paramSymbol = semanticModel.GetDeclaredSymbol(parameter);
            var references = await SymbolFinder.FindReferencesAsync(paramSymbol, document.Project.Solution, token).ConfigureAwait(false);

            var syntaxRoot = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);

            var method = parameter.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();

            if (method == null)
            {
                Debug.Assert(false, "Can'tfind method for the parameter");
                return false;
            }

            foreach (var reference in references)
            {
                var location = reference.Locations.FirstOrDefault();
                if (location.Location != null)
                {
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
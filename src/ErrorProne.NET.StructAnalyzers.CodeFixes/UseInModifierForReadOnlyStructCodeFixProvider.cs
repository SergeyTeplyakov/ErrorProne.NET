using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorProne.NET.Core;
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
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<ParameterSyntax>().FirstOrDefault();

            // It is possible for some weird cases to not have 'ParameterSyntax'. See 'WarnIfParameterIsReadOnly' in UseInModifierAnalyzer.
            if (declaration != null && !await ParameterIsUsedInNonInFriendlyManner(declaration, context.Document, context.CancellationToken).ConfigureAwait(false))
            {
                // Register a code action that will invoke the fix.
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: Title,
                        createChangedSolution: c => AddInModifier(context.Document, declaration, c),
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

        private async Task<Solution> AddInModifier(Document document, ParameterSyntax paramSyntax, CancellationToken cancellationToken)
        {
            var arguments = new Dictionary<DocumentId, List<TextSpan>>();
            var parameters = new Dictionary<DocumentId, List<TextSpan>> { { document.Id, new List<TextSpan> { paramSyntax.Span } } };

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var parameterSymbol = semanticModel.GetDeclaredSymbol(paramSyntax, cancellationToken);
            if (parameterSymbol?.ContainingSymbol is IMethodSymbol containingMethod)
            {
                var parameterIndex = containingMethod.Parameters.IndexOf(parameterSymbol);
                var parameterName = parameterSymbol.Name;

                var callers = await SymbolFinder.FindCallersAsync(containingMethod, document.Project.Solution, cancellationToken).ConfigureAwait(false);
                foreach (var caller in callers)
                {
                    foreach (var location in caller.Locations)
                    {
                        if (!location.IsInSource)
                        {
                            continue;
                        }

                        var locationRoot = await location.SourceTree.GetRootAsync(cancellationToken).ConfigureAwait(false);
                        var node = locationRoot.FindNode(location.SourceSpan, getInnermostNodeForTie: true);

                        var invocationExpression = node.Parent as InvocationExpressionSyntax;
                        if (invocationExpression is null)
                        {
                            invocationExpression = (node.Parent as MemberAccessExpressionSyntax)?.Parent as InvocationExpressionSyntax;
                        }

                        if (invocationExpression is object)
                        {
                            ArgumentSyntax? argument = null;
                            var positionalArgument = TryGetArgumentAtPosition(invocationExpression.ArgumentList, parameterIndex);
                            if (positionalArgument is object && (positionalArgument.NameColon is null || positionalArgument.NameColon.Name.Identifier.Text == parameterName))
                            {
                                argument = positionalArgument;
                            }
                            else
                            {
                                foreach (var argumentSyntax in invocationExpression.ArgumentList.Arguments)
                                {
                                    if (argumentSyntax?.NameColon.Name.Identifier.Text != parameterName)
                                    {
                                        continue;
                                    }

                                    argument = argumentSyntax;
                                    break;
                                }
                            }

                            if (argument is null)
                            {
                                continue;
                            }

                            var documentId = document.Project.Solution.GetDocument(argument.SyntaxTree)?.Id;
                            if (documentId is null)
                            {
                                continue;
                            }

                            if (!arguments.TryGetValue(documentId, out var argumentSpans))
                            {
                                argumentSpans = new List<TextSpan>();
                                arguments[documentId] = argumentSpans;
                            }

                            argumentSpans.Add(argument.Span);
                        }
                    }
                }

                var implementations = await SymbolFinder.FindImplementationsAsync(containingMethod, document.Project.Solution, projects: null, cancellationToken).ConfigureAwait(false);
                foreach (var implementation in implementations)
                {
                    foreach (var location in implementation.Locations)
                    {
                        var locationRoot = await location.SourceTree.GetRootAsync(cancellationToken).ConfigureAwait(false);
                        var node = locationRoot.FindNode(location.SourceSpan, getInnermostNodeForTie: true);
                        if (node is MethodDeclarationSyntax methodDeclaration)
                        {
                            var parameterSyntax = TryGetParameterAtPosition(methodDeclaration.ParameterList, parameterIndex);
                            if (parameterSyntax is null)
                            {
                                continue;
                            }

                            var documentId = document.Project.Solution.GetDocument(parameterSyntax.SyntaxTree)?.Id;
                            if (documentId is null)
                            {
                                continue;
                            }

                            if (!parameters.TryGetValue(documentId, out var parameterSpans))
                            {
                                parameterSpans = new List<TextSpan>();
                                parameters[documentId] = parameterSpans;
                            }

                            parameterSpans.Add(parameterSyntax.Span);
                        }
                    }
                }

                var overrides = await SymbolFinder.FindOverridesAsync(containingMethod, document.Project.Solution, projects: null, cancellationToken).ConfigureAwait(false);
                foreach (var @override in overrides)
                {
                    foreach (var location in @override.Locations)
                    {
                        var locationRoot = await location.SourceTree.GetRootAsync(cancellationToken).ConfigureAwait(false);
                        var node = locationRoot.FindNode(location.SourceSpan, getInnermostNodeForTie: true);
                        if (node is MethodDeclarationSyntax methodDeclaration)
                        {
                            var parameterSyntax = TryGetParameterAtPosition(methodDeclaration.ParameterList, parameterIndex);
                            if (parameterSyntax is null)
                            {
                                continue;
                            }

                            var documentId = document.Project.Solution.GetDocument(methodDeclaration.SyntaxTree)?.Id;
                            if (documentId is null)
                            {
                                continue;
                            }

                            if (!parameters.TryGetValue(documentId, out var parameterSpans))
                            {
                                parameterSpans = new List<TextSpan>();
                                parameters[documentId] = parameterSpans;
                            }

                            parameterSpans.Add(parameterSyntax.Span);
                        }
                    }
                }
            }

            var result = document.Project.Solution;
            foreach (var (documentId, spans) in arguments)
            {
                var originalDocument = result.GetDocument(documentId);
                var root = await originalDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                var argumentsToReplace = spans.Select(span => root.FindNode(span, getInnermostNodeForTie: true)).Select(node => node.FirstAncestorOrSelf<ArgumentSyntax>());
                if (!parameters.TryGetValue(documentId, out var parameterSpans))
                {
                    parameterSpans = new List<TextSpan>();
                }

                var parametersToReplace = parameterSpans.Select(span => root.FindNode(span, getInnermostNodeForTie: true)).Select(node => node.FirstAncestorOrSelf<ParameterSyntax>());
                var newRoot = root.ReplaceNodes(
                    argumentsToReplace.Cast<SyntaxNode>().Concat(parametersToReplace),
                    (originalNode, rewrittenNode) =>
                    {
                        if (rewrittenNode is ArgumentSyntax argument)
                        {
                            return ((ArgumentSyntax)rewrittenNode).WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.InKeyword));
                        }
                        else
                        {
                            Debug.Assert(rewrittenNode is ParameterSyntax);
                            var trivia = rewrittenNode.GetLeadingTrivia();
                            return ((ParameterSyntax)rewrittenNode)
                                .WithModifiers(((ParameterSyntax)rewrittenNode).Modifiers.Insert(0, SyntaxFactory.Token(SyntaxKind.InKeyword)))
                                .WithLeadingTrivia(trivia);
                        }
                    });

                result = result.WithDocumentSyntaxRoot(documentId, newRoot, PreservationMode.PreserveValue);
            }

            foreach (var (documentId, spans) in parameters)
            {
                if (arguments.ContainsKey(documentId))
                {
                    continue;
                }

                var originalDocument = result.GetDocument(documentId);
                var root = await originalDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                var parametersToReplace = spans.Select(span => root.FindNode(span, getInnermostNodeForTie: true)).Select(node => node.FirstAncestorOrSelf<ParameterSyntax>());
                var newRoot = root.ReplaceNodes(
                    parametersToReplace,
                    (originalNode, rewrittenNode) =>
                    {
                        var trivia = rewrittenNode.GetLeadingTrivia();
                        return rewrittenNode
                            .WithModifiers(rewrittenNode.Modifiers.Insert(0, SyntaxFactory.Token(SyntaxKind.InKeyword)))
                            .WithLeadingTrivia(trivia);
                    });

                result = result.WithDocumentSyntaxRoot(documentId, newRoot, PreservationMode.PreserveValue);
            }

            return result;
        }

        private static ParameterSyntax? TryGetParameterAtPosition(BaseParameterListSyntax? parameterList, int index)
        {
            if (parameterList is null)
            {
                return null;
            }

            if (parameterList.Parameters.Count < index)
            {
                return null;
            }

            return parameterList.Parameters[index];
        }

        private static ArgumentSyntax? TryGetArgumentAtPosition(BaseArgumentListSyntax? argumentList, int index)
        {
            if (argumentList is null)
            {
                return null;
            }

            if (argumentList.Arguments.Count < index)
            {
                return null;
            }

            return argumentList.Arguments[index];
        }
    }
}
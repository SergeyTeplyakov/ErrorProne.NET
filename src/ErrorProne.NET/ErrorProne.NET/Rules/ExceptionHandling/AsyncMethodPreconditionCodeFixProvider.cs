using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using ErrorProne.NET.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Linq;
using ErrorProne.NET.Core;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ErrorProne.NET.Rules.ExceptionHandling
{
    [ExportCodeFixProvider("AsyncMethodPreconditionCodeFixProvider", LanguageNames.CSharp), Shared]
    public sealed class AsyncMethodPreconditionCodeFixProvider : CodeFixProvider
    {
        private const string FixText = "Extract preconditions into separate non-async method";

        public override ImmutableArray<string> FixableDiagnosticIds => 
            ImmutableArray.Create(RuleIds.SuspiciousPreconditionInAsyncMethod);

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var method = context.GetFirstNodeWithDiagnostic<MethodDeclarationSyntax>(root);

            // Extracting semantic model and precodition block
            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var preconditionBlock = PreconditionsBlock.GetPreconditions(method, semanticModel);

            Contract.Assert(preconditionBlock.Preconditions.Count != 0, "Precondition block should have at least one statement!");

            // Caching all precondition. This will help to remove or leave them in different methods.
            var preconditionStatements = preconditionBlock.Preconditions.Select(p => p.IfThrowStaement).ToImmutableHashSet();
            
            // Extracting new method: it should contains all statements from the original method
            // but without preconditions.
            var extractedMethodBody = method.Body.Statements.Where(s => !preconditionStatements.Contains(s));

            // Clonning original method by changing it's body and changing the visibility
            var extractedMethod = 
                method.WithStatements(extractedMethodBody)
                        .WithIdentifier(Identifier($"Do{method.Identifier.Text}"))
                        .WithVisibilityModifier(VisibilityModifier.Private);

            // Updating original method: removing everything except preconditions
            var updatedMethodBody = method.Body.Statements.Where(s => preconditionStatements.Contains(s)).ToList();
            
            // Creating an invocation for extracted method
            var originalMethodCallExpression = CreateMethodCallExpression(extractedMethod, method.ParameterList.AsArguments());
            updatedMethodBody.Add(SyntaxFactory.ReturnStatement(originalMethodCallExpression));
            
            // Removing 'async'
            var updatedMethod =
                method.WithStatements(updatedMethodBody)
                    .WithoutModifiers(t => t.IsKind(SyntaxKind.AsyncKeyword));

            // Заменяем метод парой узлов: новым методом и выделенным методом
            var newRoot = root.ReplaceNode(method, new[] {updatedMethod, extractedMethod});
            var codeAction = CodeAction.Create(FixText, ct => Task.FromResult(context.Document.WithSyntaxRoot(newRoot)));
            context.RegisterCodeFix(codeAction, context.Diagnostics.First());
        }

        private static ExpressionSyntax CreateMethodCallExpression(MethodDeclarationSyntax method, ArgumentListSyntax arguments)
        {
            return SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(method.Identifier), arguments);
        }
    }
}
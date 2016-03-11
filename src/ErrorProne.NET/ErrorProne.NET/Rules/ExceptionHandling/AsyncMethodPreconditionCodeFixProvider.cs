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

        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RuleIds.SuspiciousPreconditionInAsyncMethod);

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();

            // Need to get method where diagnostic was reported
            var node = root.FindNode(diagnostic.Location.SourceSpan);
            var method = node.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().First();

            // Need to get precondition block of this method
            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var preconditionBlock = PreconditionsBlock.GetPreconditions(method, semanticModel);

            Contract.Assert(preconditionBlock.Preconditions.Count != 0, "Method should have at least one precondition!");

            // Extracting method that would not have preconditions, but would have all the other statements
            var preconditionStatements = preconditionBlock.Preconditions.Select(p => p.IfThrowStaement).ToImmutableHashSet();
            var extractedMethodBody = method.Body.Statements.Where(s => !preconditionStatements.Contains(s));

            // Need to change the name and make it private
            // TODO: check generated method name to avoid name conflict!
            var extractedMethod = 
                method.WithStatements(extractedMethodBody)
                      .WithIdentifier(Identifier($"Do{method.Identifier.Text}"))
                      .WithVisibilityModifier(VisibilityModifier.Private);

            // Now we need to change original method: remove method body and replace it with 
            // a method call to extracted method, and this method should no longer be async!
            
            var updatedMethodBody = method.Body.Statements.Where(s => preconditionStatements.Contains(s)).ToList();
            
            // Creating call expression for extracted method with all parameters of this method
            var originalMethodCallExpression = CreateMethodCallExpression(extractedMethod, method.ParameterList.AsArguments());

            updatedMethodBody.Add(SyntaxFactory.ReturnStatement(originalMethodCallExpression));

            var updatedMethod =
                method.WithStatements(updatedMethodBody)
                    .WithoutModifiers(t => t.IsKind(SyntaxKind.AsyncKeyword));

            // Now need to update the document:
            // 1. Replace old method with new one
            // 2. Add extracted method to the same docuemnt
            //var newRoot = root.ReplaceNode(method, updatedMethod);
            var newRoot = AppendMethodRewriter.AppendMethod(root, method, updatedMethod, extractedMethod);

            var codeAction = CodeAction.Create(FixText, ct => Task.FromResult(context.Document.WithSyntaxRoot(newRoot)));
            context.RegisterCodeFix(codeAction, diagnostic);
        }

        private static ExpressionSyntax CreateMethodCallExpression(MethodDeclarationSyntax method, ArgumentListSyntax arguments)
        {
            return SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(method.Identifier), arguments);
        }
    }
}
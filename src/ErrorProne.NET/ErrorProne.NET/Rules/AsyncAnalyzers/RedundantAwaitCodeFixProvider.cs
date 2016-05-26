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
    [ExportCodeFixProvider(nameof(RedundantAwaitCodeFixProvider), LanguageNames.CSharp), Shared]
    public sealed class RedundantAwaitCodeFixProvider : CodeFixProvider
    {
        private const string FixText = "Remove redundant await expressions";

        public override ImmutableArray<string> FixableDiagnosticIds => 
            ImmutableArray.Create(RuleIds.RedundantAwaitRule);

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var method = context.GetFirstNodeWithDiagnostic<MethodDeclarationSyntax>(root);

            // Removing 'async' keyword and removing all await expression from the body
            var newMethod = 
                RemoveAwaitSyntaxRewriter.RewriteMethod(method)
                .WithoutModifiers(t => t.IsKind(SyntaxKind.AsyncKeyword));

            // Заменяем метод парой узлов: новым методом и выделенным методом
            var newRoot = root.ReplaceNode(method, newMethod);
            var codeAction = CodeAction.Create(FixText, ct => Task.FromResult(context.Document.WithSyntaxRoot(newRoot)));
            context.RegisterCodeFix(codeAction, context.Diagnostics.First());
        }

        private class RemoveAwaitSyntaxRewriter : CSharpSyntaxRewriter
        {
            public override SyntaxNode VisitAwaitExpression(AwaitExpressionSyntax node)
            {
                return node.Expression;
            }

            public static MethodDeclarationSyntax RewriteMethod(MethodDeclarationSyntax method)
            {
                return (MethodDeclarationSyntax)(new RemoveAwaitSyntaxRewriter().Visit(method));
            }
        }
    }
}
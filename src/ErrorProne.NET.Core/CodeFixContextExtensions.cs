using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace ErrorProne.NET.Core
{
    public static class CodeFixContextExtensions
    {
        public static T GetFirstNodeWithDiagnostic<T>(this CodeFixContext context, SyntaxNode root) where T : SyntaxNode
        {
            Contract.Requires(root != null);
            Contract.Ensures(Contract.Result<T>() != null);

            var diagnostic = context.Diagnostics.First();

            var node = root.FindNode(diagnostic.Location.SourceSpan);
            return node.AncestorsAndSelf().OfType<T>().First();
        }

        public static T GetFirstNodeWithDiagnosticOrDefault<T>(this CodeFixContext context, SyntaxNode root) where T : SyntaxNode
        {
            Contract.Requires(root != null);

            var diagnostic = context.Diagnostics.First();

            var node = root.FindNode(diagnostic.Location.SourceSpan);
            return node.AncestorsAndSelf().OfType<T>().FirstOrDefault();
        }
    }
}
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.Extensions
{
    internal static class MethodDeclarationSyntaxExtensions
    {
        public static bool IsIteratorBlock(this MethodDeclarationSyntax method)
        {
            Contract.Requires(method != null);

            // very naive implementation
            return method.Body.Statements.Any(x => x is YieldStatementSyntax);
        }
    }
}
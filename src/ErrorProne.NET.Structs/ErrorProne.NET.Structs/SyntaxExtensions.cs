using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics.Contracts;
using System.Linq;

namespace ErrorProne.NET.Structs
{
    internal static class SyntaxExtensions
    {
        /// <summary>
        /// Returns true if a given <paramref name="method"/> has iterator block inside of it.
        /// </summary>
        public static bool IsIteratorBlock(this MethodDeclarationSyntax method)
        {
            Contract.Requires(method != null);

            // very naive implementation
            return method.Body?.DescendantNodes().Any(x => x is YieldStatementSyntax) == true;
        }
    }
}

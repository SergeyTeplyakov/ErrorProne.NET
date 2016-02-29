using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Extensions
{
    public static class SyntaxNodeExtensions
    {
        public static IEnumerable<SyntaxNode> EnumerateParents(this SyntaxNode node)
        {
            Contract.Requires(node != null);
            while (node.Parent != null)
            {
                yield return node.Parent;
                node = node.Parent;
            }
        }
    }
}
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Core
{
    public static class CastUtilities
    {
        public static T Cast<T>(this SyntaxNode node) where T : SyntaxNode
        {
            return (T) node;
        }
    }
}
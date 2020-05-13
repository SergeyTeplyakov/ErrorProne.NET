using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.StructAnalyzers
{
    public static class DocumentExtensions
    {
        public static Document ReplaceSyntaxRoot(this Document document, SyntaxNode? newRoot)
        {
            if (newRoot == null)
            {
                return document;
            }

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
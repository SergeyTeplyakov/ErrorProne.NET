using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Extensions
{
    public static class TypeSymbolExtensions
    {
        public static string FullName(this ITypeSymbol symbol)
        {
            return $"{symbol.ContainingNamespace}.{symbol.Name}";
        }
    }
}
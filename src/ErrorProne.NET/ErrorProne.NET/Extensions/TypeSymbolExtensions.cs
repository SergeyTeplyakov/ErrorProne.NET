using System.Diagnostics.Contracts;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Extensions
{
    public static class TypeSymbolExtensions
    {
        public static string FullName(this ITypeSymbol symbol)
        {
            Contract.Requires(symbol != null);
            return $"{symbol.ContainingNamespace}.{symbol.Name}";
        }

        public static ITypeSymbol UnwrapGenericIfNeeded(this ITypeSymbol type)
        {
            Contract.Requires(type != null);
            var named = type as INamedTypeSymbol;
            return named != null ? named.ConstructedFrom : type;
        }
    }
}
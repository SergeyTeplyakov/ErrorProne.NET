using System.Diagnostics.Contracts;
using ErrorProne.NET.Common;
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

        public static bool IsEnum(this ITypeSymbol type, SemanticModel semanticModel)
        {
            Contract.Requires(type != null);
            Contract.Requires(semanticModel != null);
            return type?.IsValueType == true && type.BaseType.Equals(semanticModel.GetClrType(typeof(System.Enum)));
        }
    }
}
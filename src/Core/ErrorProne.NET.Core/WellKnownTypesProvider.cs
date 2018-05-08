using System;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Core
{
    public static class WellKnownTypesProvider
    {
        public static INamedTypeSymbol GetExceptionType(this SemanticModel model)
        {
            return model.Compilation.GetTypeByMetadataName(typeof(Exception).FullName);
        }

        public static INamedTypeSymbol GetBoolType(this SemanticModel model)
        {
            return model.Compilation.GetTypeByMetadataName(typeof(bool).FullName);
        }

        public static INamedTypeSymbol GetObjectType(this SemanticModel model)
        {
            return model.Compilation.GetTypeByMetadataName(typeof(object).FullName);
        }

        public static INamedTypeSymbol GetClrType(this SemanticModel model, Type type)
        {
            return model.Compilation.GetTypeByMetadataName(type.FullName);
        }

        public static INamedTypeSymbol GetClrType(this Compilation compilation, Type type)
        {
            return compilation.GetClrType(type.FullName);
        }

        public static INamedTypeSymbol GetClrType(this Compilation compilation, string fullName)
        {
            return compilation.GetTypeByMetadataName(fullName);
        }

        private static readonly SymbolDisplayFormat _symbolDisplayFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        /// <summary>
        /// Returns true if the given <paramref name="type"/> belongs to a <see cref="System.Tuple"/> family of types.
        /// </summary>
        public static bool IsSystemTuple(this INamedTypeSymbol type)
        {
            // Not perfect but the simplest implementation.
            return type.IsGenericType && type.ToDisplayString(_symbolDisplayFormat) == "System.Tuple";
        }
    }
}
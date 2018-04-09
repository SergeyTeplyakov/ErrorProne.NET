using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.Structs
{
    public static class TypeExtensions
    {
        private static readonly object[] EmptyObjectsArray = new object[0];

        /// <summary>
        /// Returns true if a given type is a struct and the struct is readonly.
        /// </summary>
        public static bool IsReadOnlyStruct(this ITypeSymbol type)
        {
            if (type is INamedTypeSymbol nt)
            {
                return nt.IsReadOnlyStruct();
            }

            // This is not a named type, so we'll try to get IsReadOnly property via reflection.
            // Dirty, but can't see other options:(
            var property = type.GetType().GetRuntimeProperty("IsReadOnly");
            if (property?.GetMethod == null)
            {
                return false;
            }

            return (bool)property.GetMethod.Invoke(type, EmptyObjectsArray);
        }

        /// <summary>
        /// Returns true if a given type is a struct and the struct is readonly.
        /// </summary>
        public static bool IsReadOnlyStruct(this INamedTypeSymbol type)
        {
            if (!type.IsValueType)
            {
                return false;
            }

            // Unfortunately, IsReadOnly property is internal, so we have to compute this manually.
            foreach (var typeSyntax in type.DeclaringSyntaxReferences.Select(sr => sr.GetSyntax())
                .OfType<TypeDeclarationSyntax>())
            {
                foreach (var modifier in typeSyntax.Modifiers)
                {
                    if (modifier.Kind() == SyntaxKind.ReadOnlyKeyword)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
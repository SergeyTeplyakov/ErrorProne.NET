using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.Structs
{
    public static class TypeExtensions
    {
        /// <summary>
        /// Returns true if a given type is a struct and the struct is readonly.
        /// </summary>
        public static bool IsReadOnlyStruct(this ITypeSymbol type)
        {
            if (type.IsReferenceType)
            {
                return false;
            }

            if (type is INamedTypeSymbol nt && nt.DeclaringSyntaxReferences.Length != 0)
            {
                return nt.IsReadOnlyStruct();
            }

            // From metadata
            return type.GetAttributes().Any(a =>
                a.AttributeClass.ToDisplayString() == "System.Runtime.CompilerServices.IsReadOnlyAttribute");
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
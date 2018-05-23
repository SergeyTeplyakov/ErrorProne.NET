using System;
using System.Collections.Concurrent;
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
        private static readonly ConcurrentDictionary<Type, bool> ReadOnlyMap = new ConcurrentDictionary<Type, bool>();

        /// <summary>
        /// Returns true if a given type is a struct and the struct is readonly.
        /// </summary>
        public static bool IsReadOnlyStruct(this ITypeSymbol type)
        {
            if (type.IsReferenceType)
            {
                return false;
            }

            if (type.IsNullableType())
            {
                // Nullable types are not quite readonly, but effectively they are.
                return true;
            }

            if (type is INamedTypeSymbol nt && nt.DeclaringSyntaxReferences.Length != 0)
            {
                return nt.IsReadOnlyStruct();
            }

            // Unfortunately, there is no way to get the information about the readonliness of the type.
            // This is not a named type, so we'll try to get IsReadOnly property via reflection.
            // Dirty, but can't see other options:(
            return ReadOnlyMap.GetOrAdd(type.GetType(), t =>
            {
                var property = t.GetRuntimeProperties().FirstOrDefault(p => p.Name == "IsReadOnly");
                if (property?.GetMethod == null)
                {
                    return false;
                }

                return (bool) property.GetMethod.Invoke(type, EmptyObjectsArray);
            });
        }

        /// <summary>
        /// Returns true if the given <paramref name="type"/> is <see cref="System.Nullable{T}"/>.
        /// </summary>
        public static bool IsNullableType(this ITypeSymbol type)
        {
            var original = type.OriginalDefinition;
            return original != null && original.SpecialType == SpecialType.System_Nullable_T;
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
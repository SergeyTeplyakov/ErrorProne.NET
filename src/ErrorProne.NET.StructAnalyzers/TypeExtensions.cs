using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.StructAnalyzers
{
    public static class TypeExtensions
    {
        private static readonly ConcurrentDictionary<Type, Func<ITypeSymbol, bool>?> IsReadOnlyAccessors = new ConcurrentDictionary<Type, Func<ITypeSymbol, bool>?>();

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

            if (TryGetPropertyAccessor(IsReadOnlyAccessors, "IsReadOnly", type.GetType(), out var accessor))
            {
                return accessor(type);
            }

            if (type is INamedTypeSymbol nt && nt.DeclaringSyntaxReferences.Length != 0)
            {
                return nt.IsReadOnlyStruct();
            }

            return false;
        }

        private static bool TryGetPropertyAccessor<T, TResult>(
            ConcurrentDictionary<Type, Func<T, TResult>?> cache, string propertyName, Type type, [NotNullWhen(true)]out Func<T, TResult>? accessor)
        {
            if (cache.TryGetValue(type, out accessor))
            {
                return accessor != null;
            }

            return TryGetPropertyAccessorSlow(cache, propertyName, type, out accessor);
        }

        private static bool TryGetPropertyAccessorSlow<T, TResult>(
            ConcurrentDictionary<Type, Func<T, TResult>?> cache, string propertyName, Type type, [NotNullWhen(true)]out Func<T, TResult>? accessor)
        {
            accessor = cache.GetOrAdd(
                type,
                _ =>
                {
                    var getMethod = type.GetRuntimeProperties().FirstOrDefault(property => property.Name == propertyName)?.GetMethod;
                    if (getMethod is null)
                    {
                        return null;
                    }

                    var obj = Expression.Parameter(typeof(T), "obj");
                    var instance = !getMethod.IsStatic ? Expression.Convert(obj, getMethod.DeclaringType) : null;
                    var expr = Expression.Lambda<Func<T, TResult>>(
                        Expression.Convert(
                            Expression.Call(instance, getMethod),
                            typeof(TResult)),
                        obj);

                    return expr.Compile();
                });

            return accessor != null;
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
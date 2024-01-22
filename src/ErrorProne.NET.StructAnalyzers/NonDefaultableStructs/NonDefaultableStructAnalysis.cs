using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;

namespace ErrorProne.Net.StructAnalyzers.NonDefaultStructs
{
    /// <summary>
    /// Contains core business logic for analyzing non-defaultable structs.
    /// </summary>
    public static class NonDefaultableStructAnalysis
    {
        public const string NonDefaultableAttributeName = "NonDefaultableAttribute";

        private static readonly List<Type> SpecialTypes = new List<Type>
        {
            typeof(ImmutableArray<>)
        };

        /// <summary>
        /// Returns true if a given <paramref name="type"/> should not be created via 'default' expression.
        /// </summary>
        /// <remarks>
        /// The method covers types marked with <see cref="NonDefaultableAttributeName"/> or special types like <code>ImmutableArray{T}</code>.
        /// </remarks>
        public static bool DoNotUseDefaultConstruction(this ITypeSymbol type, Compilation compilation, out string? message)
        {
            message = null;

            var attributes = type.GetAttributes();
            var doNotUseDefaultAttribute = attributes.FirstOrDefault(a =>
                a.AttributeClass?.Name.StartsWith(NonDefaultableAttributeName) == true);

            if (doNotUseDefaultAttribute != null)
            {
                var constructorArg = doNotUseDefaultAttribute.ConstructorArguments.FirstOrDefault();
                message = !constructorArg.IsNull ? constructorArg.Value?.ToString() : null;
                return true;
            }

            if (SpecialTypes.Any(t => type.IsClrType(compilation, t)))
            {
                return true;
            }

            return false;
        }
    }
}
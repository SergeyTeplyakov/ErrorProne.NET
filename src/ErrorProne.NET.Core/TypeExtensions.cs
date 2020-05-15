using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.ContractsLight;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using ErrorProne.NET.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.Core
{
    public static class TypeExtensions
    {
        /// <summary>
        /// Returns true if the given <paramref name="type"/> is an enum.
        /// </summary>
        public static bool IsEnum(this ITypeSymbol type)
        {
            return type.TypeKind == TypeKind.Enum;
        }

        public static ITypeSymbol? GetEnumUnderlyingType(this ITypeSymbol enumType)
        {
            var namedTypeSymbol = enumType as INamedTypeSymbol;
            return namedTypeSymbol?.EnumUnderlyingType;
        }

        public static bool IsTuple(this INamedTypeSymbol type)
        {
            // Returns true for System.Tuple as well.
            return type.IsTupleType || type.IsSystemTuple();
        }

        public static IEnumerable<ITypeSymbol> GetTupleElements(this INamedTypeSymbol type)
        {
            if (type.IsSystemTuple())
            {
                foreach (var te in type.TypeArguments)
                {
                    yield return te;
                }
            }
            else
            {
                foreach (var te in type.TupleElements)
                {
                    yield return te.Type;
                }
            }
        }

        public static bool TryGetPrimitiveSize(this ITypeSymbol type, out int size)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                    size = sizeof(bool);
                    break;
                case SpecialType.System_Char:
                    size = sizeof(char);
                    break;
                case SpecialType.System_SByte:
                case SpecialType.System_Byte:
                    size = sizeof(byte);
                    break;
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                    size = sizeof(short);
                    break;
                case SpecialType.System_Single:
                    size = sizeof(float);
                    break;
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                    size = sizeof(int);
                    break;
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                    size = sizeof(long);
                    break;
                case SpecialType.System_Decimal:
                    size = sizeof(decimal);
                    break;
                case SpecialType.System_Double:
                    size = sizeof(double);
                    break;
                case SpecialType.System_DateTime:
                    size = sizeof(long);
                    break;
                default:
                    size = 0;
                    break;
            }

            return size != 0;
        }

        public static bool IsStruct([NotNullWhen(true)]this ITypeSymbol? type)
        {
            return type != null && type.IsValueType && !type.IsEnum() && !(type is ITypeParameterSymbol);
        }

        public static bool IsLargeStruct([NotNullWhen(true)]this ITypeSymbol? type, Compilation compilation, int threshold, out int estimatedSize)
        {
            estimatedSize = 0;

            if (type == null)
            {
                return false;
            }

            if (!type.IsStruct())
            {
                return false;
            }

            estimatedSize = type.ComputeStructSize(compilation);
            return estimatedSize >= threshold;
        }

        public static bool HasDefaultEqualsOrHashCodeImplementations(this ITypeSymbol type,
            out ValueTypeEqualityImplementations valueTypeEquality)
        {
            valueTypeEquality = ValueTypeEqualityImplementations.All;
            foreach (var member in type.GetMembers())
            {
                if (member.Name == nameof(Equals) && member.IsOverride)
                {
                    valueTypeEquality &= ~ValueTypeEqualityImplementations.Equals;
                }

                if (member.Name == nameof(GetHashCode) && member.IsOverride)
                {
                    valueTypeEquality &= ~ValueTypeEqualityImplementations.GetHashCode;
                }
            }

            return valueTypeEquality != ValueTypeEqualityImplementations.None;
        }

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
                // Nullable type is readonly if the underlying type is readonly
                return ((INamedTypeSymbol)type).TypeArguments[0].IsReadOnlyStruct();
            }

            return type.IsReadOnly;
        }

        /// <summary>
        /// Returns true if the given <paramref name="type"/> is <see cref="System.Nullable{T}"/>.
        /// </summary>
        public static bool IsNullableType(this ITypeSymbol type)
        {
            var original = type.OriginalDefinition;
            return original != null && original.SpecialType == SpecialType.System_Nullable_T;
        }
    }
}
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ErrorProne.NET.Utils;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Core
{
    public static class TypeExtensions
    {
        private static readonly SymbolDisplayFormat SymbolDisplayFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);
        
        /// <summary>
        /// Returns true if the given <paramref name="type"/> is an enum.
        /// </summary>
        public static bool IsEnum(this ITypeSymbol type)
        {
            return type.TypeKind == TypeKind.Enum;
        }

        /// <summary>
        /// Returns true if the given <paramref name="type"/> belongs to a <see cref="System.Tuple"/> family of types.
        /// </summary>
        public static bool IsSystemTuple(this INamedTypeSymbol type)
        {
            // Not perfect but the simplest implementation.
            return type.IsGenericType && type.ToDisplayString(SymbolDisplayFormat) == "System.Tuple";
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

        public static IEnumerable<ITypeSymbol> EnumerateBaseTypesAndSelf(this ITypeSymbol type)
        {
            var t = type;
            while (t != null)
            {
                yield return t;
                t = t.BaseType;
            }
        }

        /// <summary>
        /// Method returns true if a given <paramref name="type"/> overrides <see cref="object.ToString"/> method.
        /// </summary>
        /// <remarks>
        /// This method will return <code>false</code> if <paramref name="type"/> has a derived type that overrides <see cref="object.ToString"/>.
        /// </remarks>
        public static bool OverridesToString(this ITypeSymbol type)
        {
            if (type.IsStruct())
            {
                return type.GetMembers(nameof(ToString)).Any(m => m.IsOverride);
            }
            
            return EnumerateBaseTypesAndSelf(type)
                .Any(t => t.GetMembers(nameof(ToString)).Any(m => m.IsOverride));
        }

        /// <summary>
        /// Return true if a given <paramref name="symbol"/> derives from <paramref name="candidateBaseType"/>.
        /// </summary>
        public static bool DerivesFrom([NotNullWhen(returnValue: true)] this ITypeSymbol? symbol, [NotNullWhen(returnValue: true)] ITypeSymbol? candidateBaseType, bool baseTypesOnly = false, bool checkTypeParameterConstraints = true)
        {
            if (candidateBaseType == null || symbol == null)
            {
                return false;
            }

            if (!baseTypesOnly && candidateBaseType.TypeKind == TypeKind.Interface)
            {
                var allInterfaces = symbol.AllInterfaces.OfType<ITypeSymbol>();
                if (SymbolEqualityComparer.Default.Equals(candidateBaseType.OriginalDefinition, candidateBaseType))
                {
                    // Candidate base type is not a constructed generic type, so use original definition for interfaces.
                    allInterfaces = allInterfaces.Select(i => i.OriginalDefinition);
                }

                if (allInterfaces.Contains(candidateBaseType))
                {
                    return true;
                }
            }

            if (checkTypeParameterConstraints && symbol.TypeKind == TypeKind.TypeParameter)
            {
                var typeParameterSymbol = (ITypeParameterSymbol)symbol;
                foreach (var constraintType in typeParameterSymbol.ConstraintTypes)
                {
                    if (constraintType.DerivesFrom(candidateBaseType, baseTypesOnly, checkTypeParameterConstraints))
                    {
                        return true;
                    }
                }
            }

            while (symbol != null)
            {
                if (SymbolEqualityComparer.Default.Equals(symbol, candidateBaseType))
                {
                    return true;
                }

                symbol = symbol.BaseType;
            }

            return false;
        }

        /// <summary>
        /// Indicates if the given <paramref name="type"/> is disposable,
        /// and thus can be used in a <code>using</code> or <code>await using</code> statement.
        /// </summary>
        public static bool IsDisposable(this ITypeSymbol type,
            INamedTypeSymbol? iDisposable,
            INamedTypeSymbol? iAsyncDisposable,
            INamedTypeSymbol? configuredAsyncDisposable)
        {
            if (type.IsReferenceType)
            {
                return IsInterfaceOrImplementsInterface(type, iDisposable)
                       || IsInterfaceOrImplementsInterface(type, iAsyncDisposable);
            }
            else if (SymbolEqualityComparer.Default.Equals(type, configuredAsyncDisposable))
            {
                return true;
            }

            if (type.IsRefLikeType)
            {
                return type.GetMembers("Dispose").OfType<IMethodSymbol>()
                    .Any(method => method.HasDisposeSignatureByConvention());
            }

            return false;

            static bool IsInterfaceOrImplementsInterface(ITypeSymbol type, INamedTypeSymbol? interfaceType)
                => interfaceType != null &&
                   (SymbolEqualityComparer.Default.Equals(type, interfaceType) || type.AllInterfaces.Contains(interfaceType));
        }

    }
}
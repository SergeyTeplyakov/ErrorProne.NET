using System;
using System.Diagnostics.Contracts;
using System.Linq;
using ErrorProne.NET.Common;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Extensions
{
    public static class TypeSymbolExtensions
    {
        public static string FullName(this ITypeSymbol symbol)
        {
            Contract.Requires(symbol != null);
            var symbolDisplayFormat = new SymbolDisplayFormat(
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);
            return symbol.ToDisplayString(symbolDisplayFormat);
        }

        public static string Defaultvalue(this ITypeSymbol symbol)
        {
            if (symbol.IsReferenceType)
            {
                return "null";
            }

            if (symbol.IsEnum())
            {
                var v = symbol.GetMembers().OfType<IFieldSymbol>().FirstOrDefault();
                if (v == null || Convert.ToInt32(v.ConstantValue) != 0)
                {
                    return "0";
                }

                return v.Name;
            }

            if (symbol.SpecialType == SpecialType.None)
            {
                return $"default({symbol.Name})";
            }

            var clrType = Type.GetType(symbol.FullName());
            return Activator.CreateInstance(clrType).ToString();
        }

        public static ITypeSymbol UnwrapGenericIfNeeded(this ITypeSymbol type)
        {
            Contract.Requires(type != null);
            var named = type as INamedTypeSymbol;
            return named != null ? named.ConstructedFrom : type;
        }

        public static bool IsEnum(this ITypeSymbol type)
        {
            Contract.Requires(type != null);
            return type?.IsValueType == true &&
                   type.BaseType.SpecialType == SpecialType.System_Enum;
        }

        public static ITypeSymbol GetEnumUnderlyingType(this ITypeSymbol enumType)
        {
            var namedTypeSymbol = enumType as INamedTypeSymbol;
            return namedTypeSymbol?.EnumUnderlyingType;
        }

        public static bool IsNullableEnum(this ITypeSymbol type, SemanticModel semanticModel)
        {
            Contract.Requires(type != null);
            Contract.Requires(semanticModel != null);

            return type.UnwrapFromNullableType(semanticModel)?.IsEnum() == true;
        }

        public static bool IsNullablePrimitiveType(this ITypeSymbol type, SemanticModel semanticModel)
        {
            Contract.Requires(type != null);
            Contract.Requires(semanticModel != null);

            return type.UnwrapFromNullableType(semanticModel)?.IsPrimitiveType() == true;
        }

        public static ITypeSymbol UnwrapFromNullableType(this ITypeSymbol type, SemanticModel semanticModel)
        {
            var namedType = type as INamedTypeSymbol;
            if (namedType == null) return null;
            if (type.UnwrapGenericIfNeeded().Equals(semanticModel.GetClrType(typeof (Nullable<>))))
            {
                return namedType.TypeArguments[0];
            }

            return null;
        }

        public static bool IsPrimitiveType(this ITypeSymbol type)
        {
            Contract.Requires(type != null);
            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_Char:
                case SpecialType.System_SByte:
                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Decimal:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_DateTime:
                    return true;
            }

            return false;
        }

        public static bool TryGetPrimitiveSize(this ITypeSymbol type, out int size)
        {
            Contract.Requires(type != null);
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
    }
}
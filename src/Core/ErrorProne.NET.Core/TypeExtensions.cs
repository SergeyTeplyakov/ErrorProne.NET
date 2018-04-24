using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Utils
{
    public static class TypeExtensions
    {
        /// <summary>
        /// Returns true if the given <paramref name="type"/> is an enum.
        /// </summary>
        public static bool IsEnum(this ITypeSymbol type)
        {
            Debug.Assert(type != null);
            return type.TypeKind == TypeKind.Enum;
        }

        public static ITypeSymbol GetEnumUnderlyingType(this ITypeSymbol enumType)
        {
            Debug.Assert(enumType != null);

            var namedTypeSymbol = enumType as INamedTypeSymbol;
            return namedTypeSymbol?.EnumUnderlyingType;
        }

        public static bool TryGetPrimitiveSize(this ITypeSymbol type, out int size)
        {
            Debug.Assert(type != null);
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
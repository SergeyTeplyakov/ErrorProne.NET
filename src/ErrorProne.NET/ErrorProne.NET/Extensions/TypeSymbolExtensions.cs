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

        public static bool IsEnum(this ITypeSymbol type, SemanticModel semanticModel = null)
        {
            Contract.Requires(type != null);
            //Contract.Requires(semanticModel != null);
            return type?.IsValueType == true &&
                   type.BaseType.SpecialType == SpecialType.System_Enum;
                //type.BaseType.Equals(semanticModel.GetClrType(typeof (System.Enum)));
        }
    }
}
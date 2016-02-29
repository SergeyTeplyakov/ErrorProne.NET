using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Extensions
{
    public static class NamedSymbolExtensions
    {
        public static bool HasAttribute(this INamedTypeSymbol symbol, Type attributeType)
        {
            Contract.Requires(symbol != null);
            Contract.Requires(attributeType != null);

            return symbol.GetAttributes().Any(a => a.AttributeClass.FullName() == attributeType.FullName);
        }

        public static IEnumerable<INamedTypeSymbol> TraverseTypeAndItsBaseTypes(this INamedTypeSymbol symbol)
        {
            Contract.Requires(symbol != null);

            yield return symbol;
            while (symbol.BaseType != null)
            {
                yield return symbol.BaseType;
                symbol = symbol.BaseType;
            }
        }

        public static bool IsType(this INamedTypeSymbol namedType, Type type)
        {
            return namedType.MetadataName == type.AssemblyQualifiedName;
        }

        public static string GetTypeQualifiedAssemblyName(this INamedTypeSymbol namedType)
        {
            return BuildQualifiedAssemblyName(null, namedType.ToDisplayString(), namedType.ContainingAssembly);
        }

        public static List<Tuple<IFieldSymbol, long>> GetSortedEnumFieldsAndValues(this INamedTypeSymbol enumType)
        {
            Contract.Requires(enumType != null);

            var result = new List<Tuple<IFieldSymbol, long>>();
            var underlyingSpecialType = enumType.EnumUnderlyingType.SpecialType;
            foreach (var member in enumType.GetMembers())
            {
                if (member.Kind == SymbolKind.Field)
                {
                    var field = (IFieldSymbol)member;
                    if (field.HasConstantValue)
                    {
                        var value = (long)ConvertEnumUnderlyingTypeToUInt64(field.ConstantValue, underlyingSpecialType);
                        result.Add(Tuple.Create(field, value));
                    }
                }
            }

            return result.OrderBy(e => e.Item2).ToList();
        }

        internal static ulong ConvertEnumUnderlyingTypeToUInt64(object value, SpecialType specialType)
        {
            Contract.Requires(value != null);
            Contract.Requires(value.GetType().GetTypeInfo().IsPrimitive);

            unchecked
            {
                switch (specialType)
                {
                    case SpecialType.System_SByte:
                        return (ulong)(sbyte)value;
                    case SpecialType.System_Int16:
                        return (ulong)(short)value;
                    case SpecialType.System_Int32:
                        return (ulong)(int)value;
                    case SpecialType.System_Int64:
                        return (ulong)(long)value;
                    case SpecialType.System_Byte:
                        return (byte)value;
                    case SpecialType.System_UInt16:
                        return (ushort)value;
                    case SpecialType.System_UInt32:
                        return (uint)value;
                    case SpecialType.System_UInt64:
                        return (ulong)value;

                    default:
                        // not using ExceptionUtilities.UnexpectedValue() because this is used by the Services layer
                        // which doesn't have those utilities.
                        throw new InvalidOperationException($"{specialType} is not a valid underlying type for an enum");
                }
            }
        }

        private static string BuildQualifiedAssemblyName(string nameSpace, string typeName, IAssemblySymbol assemblySymbol)
        {
            var symbolType = string.IsNullOrEmpty(nameSpace) ? typeName : $"{nameSpace}.{typeName}";

            return $"{symbolType}, {new AssemblyName(assemblySymbol.Identity.GetDisplayName(true))}";
        }

        public static bool IsDerivedFromInterface(this INamedTypeSymbol namedType, Type type)
        {
            Contract.Requires(namedType != null);
            Contract.Requires(type != null);

            return Enumerable.Any(namedType.AllInterfaces, symbol => symbol.IsType(type));
        }

        public static bool IsExceptionType(this ISymbol symbol, SemanticModel model)
        {
            var namedSymbol = symbol as INamedTypeSymbol;
            if (namedSymbol == null)
            {
                return false;
            }

            var exceptionType = model.Compilation.GetTypeByMetadataName(typeof(Exception).FullName);

            return TraverseTypeAndItsBaseTypes(namedSymbol).Any(x => x.Equals(exceptionType));
        }


    }
}
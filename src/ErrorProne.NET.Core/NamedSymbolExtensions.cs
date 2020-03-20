using System;
using System.Collections.Generic;
using System.Diagnostics.ContractsLight;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Extensions
{
    public static class NamedSymbolExtensions
    {
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
            //return namedType.MetadataName == type.AssemblyQualifiedName;
            return namedType.GetTypeQualifiedAssemblyName() == type.AssemblyQualifiedName;
        }

        public static string GetTypeQualifiedAssemblyName(this INamedTypeSymbol namedType)
        {
            return BuildQualifiedAssemblyName(null, namedType.ToDisplayString(), namedType.ContainingAssembly);
        }

        public static List<Tuple<IFieldSymbol, long>> GetSortedEnumFieldsAndValues(this INamedTypeSymbol enumType)
        {
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
                return specialType switch
                {
                    SpecialType.System_SByte => (ulong)(sbyte)value,
                    SpecialType.System_Int16 => (ulong)(short)value,
                    SpecialType.System_Int32 => (ulong)(int)value,
                    SpecialType.System_Int64 => (ulong)(long)value,
                    SpecialType.System_Byte => (byte)value,
                    SpecialType.System_UInt16 => (ushort)value,
                    SpecialType.System_UInt32 => (uint)value,
                    SpecialType.System_UInt64 => (ulong)value,
                    _ => throw new InvalidOperationException($"{specialType} is not a valid underlying type for an enum"),// not using ExceptionUtilities.UnexpectedValue() because this is used by the Services layer
                };
            }
        }

        private static string BuildQualifiedAssemblyName(string? nameSpace, string typeName, IAssemblySymbol assemblySymbol)
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
            if (!(symbol is INamedTypeSymbol namedSymbol))
            {
                return false;
            }

            var exceptionType = model.Compilation.GetTypeByMetadataName(typeof(Exception).FullName);

            return TraverseTypeAndItsBaseTypes(namedSymbol).Any(x => x.Equals(exceptionType));
        }

        public static bool IsArgumentExceptionType(this ISymbol symbol, SemanticModel model)
        {
            if (!(symbol is INamedTypeSymbol namedSymbol))
            {
                return false;
            }

            var exceptionType = model.Compilation.GetTypeByMetadataName(typeof(ArgumentException).FullName);

            return TraverseTypeAndItsBaseTypes(namedSymbol).Any(x => x.Equals(exceptionType));
        }
    }
}
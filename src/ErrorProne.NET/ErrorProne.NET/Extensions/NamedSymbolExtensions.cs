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
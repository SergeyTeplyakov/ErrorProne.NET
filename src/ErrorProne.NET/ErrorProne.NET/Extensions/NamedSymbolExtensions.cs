using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
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
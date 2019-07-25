using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using System.Linq;

namespace ErrorProne.NET.Core
{
    public static class ValueTupleHelper
    {
        public static ITypeSymbol[] GetTupleTypes(this ITypeSymbol tupleType)
        {
            var tuple = (TupleTypeSymbol)tupleType;
            return tuple.TupleElementTypes.ToArray();
        }
    }
}

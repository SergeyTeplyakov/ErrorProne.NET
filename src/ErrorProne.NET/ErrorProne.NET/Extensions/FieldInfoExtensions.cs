using System.Diagnostics.Contracts;
using System.Linq;
using ErrorProne.NET.Annotations;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Extensions
{
    public static class FieldInfoExtensions
    {
        public static bool HasReadOnlyAttribute(this IFieldSymbol fieldSymbol)
        {
            Contract.Requires(fieldSymbol != null);

            return fieldSymbol.GetAttributes().Any(a => a.AttributeClass.FullName() == typeof (ReadOnlyAttribute).FullName);
        }
    }
}
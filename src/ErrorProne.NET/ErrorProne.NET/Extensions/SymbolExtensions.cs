using System;
using System.Diagnostics.Contracts;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Extensions
{
    public static class SymbolExtensions
    {
        public static IMethodSymbol GetBaseMostOverridenMethod(this IMethodSymbol method)
        {
            Contract.Requires(method != null);
            Contract.Requires(method.IsOverride);
            Contract.Ensures(Contract.Result<IMethodSymbol>() != null);

            while (method.OverriddenMethod != null)
            {
                if (method.OverriddenMethod.OverriddenMethod == null)
                {
                    return method.OverriddenMethod;
                }

                method = method.OverriddenMethod;
            }

            return null;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Extensions
{
    public static class SymbolExtensions
    {
        /// <summary>
        /// Returns the very first virtual/abstract method in the inheritance chain.
        /// </summary>
        public static IMethodSymbol GetBaseMostOverridenMethod(this IMethodSymbol method)
        {
            Contract.Requires(method != null);
            Contract.Requires(method.IsOverride);
            Contract.Ensures(Contract.Result<IMethodSymbol>() != null);

            return method.GetBaseMethodsChain().Last();
        }

        public static ImmutableArray<IMethodSymbol> MethodAndFullInheritanceChain(this IMethodSymbol method)
        {
            return
                ImmutableArray.Create(method)
                    .AddRange(method.GetBaseMethodsChain())
                    .AddRange(method.GetImplementedInterfaceMethods());
        }

        /// <summary>
        /// Returns all base methods for the <paramref name="method"/>.
        /// </summary>
        public static IEnumerable<IMethodSymbol> GetBaseMethodsChain(this IMethodSymbol method)
        {
            while (method.OverriddenMethod != null)
            {
                if (method.OverriddenMethod.OverriddenMethod == null)
                {
                    yield return method.OverriddenMethod;
                }

                method = method.OverriddenMethod;
            }
        }

        public static IEnumerable<IMethodSymbol> GetImplementedInterfaceMethods(this IMethodSymbol method)
        {
            // Not sure about performance of this stuff!!
            var members = method.ContainingType.AllInterfaces
                    .Select(i => i)
                    .SelectMany(interfaceType => interfaceType.GetMembers().OfType<IMethodSymbol>()).ToList();

            return members
                .Where(interfaceMethod => method.Equals(method.ContainingType.FindImplementationForInterfaceMember(interfaceMethod)));
        }
    }
}
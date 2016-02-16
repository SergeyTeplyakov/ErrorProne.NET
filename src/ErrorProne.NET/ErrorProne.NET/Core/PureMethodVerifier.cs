using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using ErrorProne.NET.Common;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.Core
{
    public static class PureMethodVerifier
    {
        //private static HashSet<INamedTypeSymbol> _immutableTypes;
        private static HashSet<string> _pureMethodNames = CreatePureMethodNames();
        private static HashSet<string> _immutableTypes = CreateImmutableTypes();

        private static HashSet<string> CreatePureMethodNames()
        {
            return new HashSet<string>()
            {
                "ToString",
                "GetHashCode",
                "Equals"
            };
        }

        private static HashSet<string> CreateImmutableTypes()
        {
            return new HashSet<string>()
            {
                typeof(string).FullName,
                typeof(Delegate).FullName,
                typeof(object).FullName,
                typeof(Enum).FullName,
                typeof(Type).FullName,
            };
        }

        public static bool IsPure(this InvocationExpressionSyntax methodInvocation, SemanticModel semanticModel)
        {
            Contract.Requires(methodInvocation != null);
            Contract.Requires(semanticModel != null);

            var symbol = semanticModel.GetSymbolInfo(methodInvocation).Symbol as IMethodSymbol;
            if (symbol == null || symbol.ReturnsVoid)
            {
                return false;
            }

            if (HasPureAttribute(symbol, semanticModel))
            {
                return true;
            }

            if (IsStaticOnStruct(symbol))
            {
                return true;
            }

            if (IsReceiverImmutable(symbol))
            {
                return true;
            }

            if (IsImmutableExtensionMethod(symbol))
            {
                return true;
            }

            if (MethodFromWellKnownImmutableCoreTypes(symbol, semanticModel))
            {
                return true;
            }

            if (IsRoslynApi(symbol) && ReturnsTheSameType(symbol))
            {
                return true;
            }

            if (WithPattern(symbol) && ReturnsTheSameType(symbol))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if method is most likely pure, or result is useful enough for caller to consume.
        /// </summary>
        public static bool IsPureCandidate(this InvocationExpressionSyntax methodInvocation, SemanticModel semanticModel)
        {
            return false;
        }

        private static bool WithPattern(IMethodSymbol symbol)
        {
            return symbol.Name.StartsWith("With", StringComparison.Ordinal);
        }

        private static bool MethodFromWellKnownImmutableCoreTypes(IMethodSymbol symbol, SemanticModel model)
        {
            if (symbol.IsOverride)
            {
                // Can't just use symbol.OverriddenMethod, because the same method could be overriden multiple types!
                // Hint: R# pure method invocation analysis for methods from IEquatable are not working on portable project!!
                //symbol.GetBaseMostOverridenMethod().ContainingType.Equals(null);

                if (symbol.GetBaseMostOverridenMethod().ContainingType.Equals(model.GetClrType(typeof (object))))
                {
                    return true;
                }
            }
            else
            {
                // Could be two cases: 
                // 1. Static type could be of the well-known interface type or
                // 2. Method is an implementation of the well-known interface type

                var wellKnownImmutableInterfaces = GetWellKnownImmutableSystemTypes(model);

                if (wellKnownImmutableInterfaces.Contains(symbol.ContainingType.ConstructedFrom))
                {
                    // First case: calling method of the well-known immutable interface type
                    return true;
                }

                var members = symbol.ContainingType.AllInterfaces
                    .Select(i => i)
                    .Where(i => wellKnownImmutableInterfaces.Contains(i.ConstructedFrom))
                    .SelectMany(interfaceType => interfaceType.GetMembers().OfType<IMethodSymbol>()).ToList();

                return members
                    .Any(interfaceMethod => symbol.Equals(symbol.ContainingType.FindImplementationForInterfaceMember(interfaceMethod)));
            }

            return false;
        }

        private static HashSet<INamedTypeSymbol> GetWellKnownImmutableSystemTypes(SemanticModel model)
        {
            return new HashSet<INamedTypeSymbol>()
            {
                model.GetClrType(typeof(IEquatable<>)),
                model.GetClrType(typeof(IComparable<>)),
                model.GetClrType(typeof(IFormattable)),
                //model.GetClrType(typeof(IConvertible)),
                model.GetClrType(typeof(ICustomFormatter)),

                //model.GetClrType(typeof(ICloneable)),
            };
        }

        // TODO: not sure about this approach because it could be too agreesive!
        private static bool IsRoslynApi(IMethodSymbol symbol)
        {
            return symbol.ContainingNamespace.ToDisplayString().Contains("Microsoft.CodeAnalysis");
        }

        private static bool IsReceiverImmutable(IMethodSymbol symbol)
        {
            if (symbol.ReceiverType.Name.StartsWith("Immutable", StringComparison.Ordinal))
            {
                return true;
            }

            if (_immutableTypes.Contains(symbol.ReceiverType.FullName()))
            {
                return true;
            }

            return false;
        }

        private static bool IsStaticOnStruct(IMethodSymbol symbol)
        {
            return symbol.ReceiverType.IsValueType && symbol.IsStatic;
        }

        private static bool IsImmutableExtensionMethod(IMethodSymbol symbol)
        {
            if (symbol.IsExtensionMethod)
            {
                // If this is an extension method and there is no additional parameters, then
                // client should better observer the result!
                if (symbol.Parameters.Length == 0)
                {
                    return true;
                }

                return ReturnsTheSameType(symbol);
            }

            return false;
        }

        private static bool ReturnsTheSameType(IMethodSymbol symbol)
        {
            // it is ok, if return type and the first argument (i.e. this argument) are differs only
            // with generic type arguments, like for Enumerable.Select
            if (symbol.ReturnType.Equals(symbol.ReceiverType))
            {
                return true;
            }

            if (symbol.IsGenericMethod)
            {
                return (symbol.ReturnType as INamedTypeSymbol)?.ConstructedFrom?.Equals(
                    (symbol.ReceiverType as INamedTypeSymbol)?.ConstructedFrom) == true;
            }

            return false;
        }

        private static bool HasPureAttribute(IMethodSymbol symbol, SemanticModel model)
        {
            var pureAttribute = model.Compilation.GetTypeByMetadataName(typeof(PureAttribute).FullName);
            var attributes = symbol.GetAttributes();

            return attributes.Any(a => a.AttributeClass.Equals(pureAttribute));
        }

        private static HashSet<INamedTypeSymbol> GetImmutableTypes(SemanticModel model)
        {
            return null;
        }
    }
}
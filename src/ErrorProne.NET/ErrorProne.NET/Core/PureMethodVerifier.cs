using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        private static HashSet<INamedTypeSymbol> GetWellKnownImmutableSystemTypes(SemanticModel model)
        {
            return new HashSet<INamedTypeSymbol>()
            {
                model.GetClrType(typeof(object)),
                model.GetClrType(typeof(Delegate)),
                model.GetClrType(typeof(string)),
                model.GetClrType(typeof(Enum)),
                model.GetClrType(typeof(Type)),

                model.GetClrType(typeof(IEquatable<>)),
                model.GetClrType(typeof(IComparable<>)),
                model.GetClrType(typeof(IFormattable)),
                model.GetClrType(typeof(IEnumerable<>)),
                model.GetClrType(typeof(IQueryable<>)),
                model.GetClrType(typeof(ICustomFormatter)),
            };
        }

        public static bool IsPure(this InvocationExpressionSyntax methodInvocation, SemanticModel semanticModel)
        {
            Contract.Requires(methodInvocation != null);
            Contract.Requires(semanticModel != null);

            var symbol = semanticModel.GetSymbolInfo(methodInvocation).Symbol as IMethodSymbol;
            // If method has out or ref param the return value could be ignored!
            // TODO: this logic has to be moved out of this method!
            if (symbol == null || symbol.ReturnsVoid || symbol.Parameters.Any(p => p.RefKind == RefKind.Out || p.RefKind == RefKind.Ref))
            {
                return false;
            }

            ImmutableArray<IMethodSymbol> methodChain = symbol.MethodAndFullInheritanceChain();

            if (HasPureAttribute(methodChain, semanticModel))
            {
                return true;
            }

            if (IsStaticOnStruct(symbol))
            {
                return true;
            }

            if (IsImmutableMemberCall(symbol, methodChain, semanticModel))
            {
                return true;
            }

            if (WithPattern(symbol) && ReturnsTheSameType(symbol))
            {
                return true;
            }

            return false;
        }
        private static bool WithPattern(IMethodSymbol symbol)
        {
            return symbol.Name.StartsWith("With", StringComparison.Ordinal);
        }

        private static bool IsImmutableMemberCall(IMethodSymbol symbol, ImmutableArray<IMethodSymbol> baseMethodsChain, SemanticModel model)
        {
            if (symbol.ReceiverType.Name.StartsWith("Immutable", StringComparison.Ordinal))
            {
                return true;
            }

            HashSet<INamedTypeSymbol> immutableTypes = GetWellKnownImmutableSystemTypes(model);

            // If method is an extension method and method extends an immutable type, then the method is pure
            if (symbol.IsExtensionMethod && immutableTypes.Contains(symbol.ReceiverType.UnwrapGenericIfNeeded()))
            {
                return true;
            }
            
            if (baseMethodsChain.Any(m => immutableTypes.Contains(m.ContainingType.UnwrapGenericIfNeeded())))
            {
                return true;
            }

            // Check whether method is a factory method that uses only primitive types
            if (symbol.IsStatic && symbol.Parameters.All(p => IsImmutableOrPrimitive(immutableTypes, p.Type)))
            {
                return true;
            }

            return false;
        }

        private static bool IsImmutableOrPrimitive(HashSet<INamedTypeSymbol> immutableTypes, ITypeSymbol parameterType)
        {
            if (parameterType.Name.StartsWith("Immutable"))
            {
                return true;
            }
            if (immutableTypes.Contains(parameterType.UnwrapGenericIfNeeded()))
            {
                return true;
            }

            // Consider that all valud types in System namespace are pure!
            if (parameterType.IsValueType && parameterType.ContainingNamespace.Name == "System")
            {
                return true;
            }

            return false;
        }

        private static bool IsStaticOnStruct(IMethodSymbol symbol)
        {
            return symbol.ReceiverType.IsValueType && symbol.IsStatic;
        }

        private static bool ReturnsTheSameType(IMethodSymbol symbol)
        {
            // Need to unwrap generics to get unsinstantiated types if needed
            return symbol.ReturnType.UnwrapGenericIfNeeded().Equals(symbol.ReceiverType.UnwrapGenericIfNeeded());
        }

        private static bool HasPureAttribute(ImmutableArray<IMethodSymbol> methodChain, SemanticModel model)
        {
            var pureAttribute = model.Compilation.GetTypeByMetadataName(typeof(PureAttribute).FullName);

            return methodChain.SelectMany(m => m.GetAttributes()).Any(a => a.AttributeClass.Equals(pureAttribute));
        }
    }
}
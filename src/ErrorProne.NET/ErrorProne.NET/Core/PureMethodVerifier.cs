using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using ErrorProne.NET.Common;
using ErrorProne.NET.Extensions;
using ErrorProne.NET.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.Core
{
    internal class PureMethodVerifier
    {
        private readonly SemanticModel _semanticModel;
        private readonly Lazy<HashSet<INamedTypeSymbol>> _wellKnownImmutableTypes;
        private readonly Lazy<HashSet<INamedTypeSymbol>> _wellKnownFactoryTypes;

        public PureMethodVerifier(SemanticModel semanticModel)
        {
            Contract.Requires(semanticModel != null);
            _semanticModel = semanticModel;

            _wellKnownImmutableTypes = LazyEx.Create(() => GetWellKnownImmutableSystemTypes(semanticModel));
            _wellKnownFactoryTypes = LazyEx.Create(() => GetWellKnownFactories(semanticModel));
        }

        public bool IsImmutable(ITypeSymbol symbol)
        {
            Contract.Requires(symbol != null);

            if (symbol.Name.StartsWith("Immutable", StringComparison.Ordinal))
            {
                return true;
            }

            // If method is an extension method and method extends an immutable type, then the method is pure
            if (_wellKnownImmutableTypes.Value.Contains(symbol.UnwrapGenericIfNeeded()))
            {
                return true;
            }

            // Consider that all valud types in System namespace are pure!
            if (symbol.IsValueType && symbol.ContainingNamespace.Name == "System")
            {
                return true;
            }

            return false;
        }

        public bool IsPure(InvocationExpressionSyntax methodInvocation)
        {
            Contract.Requires(methodInvocation != null);

            var symbol = _semanticModel.GetSymbolInfo(methodInvocation).Symbol as IMethodSymbol;

            return symbol != null && IsPure(symbol);
        }

        public bool IsPure(IMethodSymbol symbol)
        {
            Contract.Requires(symbol != null);

            // If method has out or ref param the return value could be ignored!

            if (symbol.ReturnsVoid || symbol.Parameters.Any(p => p.RefKind == RefKind.Out || p.RefKind == RefKind.Ref))
            {
                return false;
            }

            ImmutableArray<IMethodSymbol> methodChain = symbol.MethodAndFullInheritanceChain();

            if (HasPureAttribute(methodChain))
            {
                return true;
            }

            if (IsStaticOnStruct(symbol))
            {
                return true;
            }

            if (IsImmutableMemberCall(symbol, methodChain))
            {
                return true;
            }

            if (IsFactoryMethod(symbol))
            {
                return true;
            }

            if (WithPattern(symbol) && ReturnsTheSameType(symbol))
            {
                return true;
            }

            return false;
        }

        private HashSet<INamedTypeSymbol> GetWellKnownImmutableSystemTypes(SemanticModel model)
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

        private HashSet<INamedTypeSymbol> GetWellKnownFactories(SemanticModel model)
        {
            return new HashSet<INamedTypeSymbol>()
            {
                model.GetClrType(typeof(Enumerable)),
                model.GetClrType(typeof(Queryable)),
                model.GetClrType(typeof(Convert)),
            };
        }

        private bool IsFactoryMethod(IMethodSymbol symbol)
        {
            if (symbol.IsStatic)
            {
                return _wellKnownFactoryTypes.Value.Contains(symbol.ContainingType) ||
                       _wellKnownImmutableTypes.Value.Contains(symbol.ContainingType);
            }

            return false;
        }

        private static bool WithPattern(IMethodSymbol symbol)
        {
            return symbol.Name.StartsWith("With", StringComparison.Ordinal);
        }

        private bool IsImmutableMemberCall(IMethodSymbol symbol, ImmutableArray<IMethodSymbol> baseMethodsChain)
        {
            if (IsImmutable(symbol.ReceiverType))
            {
                return true;
            }

            if (baseMethodsChain.Any(b => _wellKnownImmutableTypes.Value.Contains(b.ReceiverType.UnwrapGenericIfNeeded())))
            {
                return true;
            }

            // If method is an extension method and method extends an immutable type, then the method is pure
            if (symbol.IsExtensionMethod && _wellKnownImmutableTypes.Value.Contains(symbol.ReceiverType.UnwrapGenericIfNeeded()))
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

        private bool HasPureAttribute(ImmutableArray<IMethodSymbol> methodChain)
        {
            var pureAttribute = _semanticModel.Compilation.GetTypeByMetadataName(typeof(PureAttribute).FullName);

            return methodChain.SelectMany(m => m.GetAttributes()).Any(a => a.AttributeClass.Equals(pureAttribute));
        }
    }
}
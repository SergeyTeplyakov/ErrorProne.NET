using System.Diagnostics.Contracts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.Core
{
    public static class PureMethodExtensions
    {
        [Pure]
        public static bool IsPure(this InvocationExpressionSyntax methodInvocation, SemanticModel semanticModel)
        {
            Contract.Requires(methodInvocation != null);
            Contract.Requires(semanticModel != null);

            return new PureMethodVerifier(semanticModel).IsPure(methodInvocation);
        }

        public static bool IsImmutable(this ITypeSymbol symbol, SemanticModel semanticModel)
        {
            Contract.Requires(symbol != null);
            Contract.Requires(semanticModel != null);

            return new PureMethodVerifier(semanticModel).IsImmutable(symbol);
        }
    }
}
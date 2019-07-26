using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#nullable enable

namespace ErrorProne.NET.Core
{
    public static class LocalScopeProvider
    {
        public static SyntaxNode? GetScopeForDisplayClass(this ISymbol symbol)
        {
            // This method uses internal Roslyn API

            if (symbol is SourceLocalSymbol localSymbol)
            {
                // For loop is special: a local declared in for-loop is scoped to the loop based on
                // the language rules, but at "runtime" it actually kind of defined in the enclosing scope.
                // It means that if an anonymous method captures a for variable, a display class is instantiated
                // at the beginning of a parent scope, but not inside the for loop.

                if (localSymbol.ScopeBinder is ForLoopBinder fb)
                {
                    return fb.Next.ScopeDesignator;
                }

                return localSymbol.ScopeDesignatorOpt;
            }

            if (symbol is IParameterSymbol)
            {
                var declaredIn = symbol.ContainingSymbol;
                var (blockBody, arrowBody) = declaredIn.GetBodies();
                return (SyntaxNode)blockBody ?? arrowBody;
            }

            return null;
        }

        internal static (BlockSyntax blockBody, ArrowExpressionClauseSyntax arrowBody) GetBodies(this ISymbol methodSymbol)
        {
            var syntax = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();

            {
                switch (syntax)
                {
                    case BaseMethodDeclarationSyntax method:
                        return (method.Body, method.ExpressionBody);

                    case AccessorDeclarationSyntax accessor:
                        return (accessor.Body, accessor.ExpressionBody);

                    case ArrowExpressionClauseSyntax arrowExpression:
                        Debug.Assert(arrowExpression.Parent.Kind() == SyntaxKind.PropertyDeclaration ||
                                     arrowExpression.Parent.Kind() == SyntaxKind.IndexerDeclaration ||
                                     methodSymbol is SynthesizedClosureMethod);
                        return (null, arrowExpression);

                    case BlockSyntax block:
                        Debug.Assert(methodSymbol is SynthesizedClosureMethod);
                        return (block, null);

                    default:
                        return (null, null);
                }
            }
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.AsyncAnalyzers
{
    public enum NoHiddenAllocationsLevel
    {
        Default,
        Recursive,
    }

    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class NoHiddenAllocationsAttribute : Attribute
    {

    }

    public static class NoHiddenAllocationsConfiguration
    {
        private static string AttributeName = "NoHiddenAllocations";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="node"></param>
        /// <param name="semanticModel"></param>
        /// <returns></returns>
        public static NoHiddenAllocationsLevel? TryGetConfiguration(SyntaxNode node, SemanticModel semanticModel)
        {
            // The assembly can have the attribute, or any of the node's ancestors
            if (ContainsAttribute(semanticModel.Compilation.Assembly.GetAttributes(), AttributeName))
            {
                return NoHiddenAllocationsLevel.Default;
            }

            var operation = semanticModel.GetOperation(node);

            // Try and find an enclosing method declaration
            var enclosingMethodBodyOperation =
                operation?.AncestorAndSelf().FirstOrDefault(o => o is IMethodBodyBaseOperation);

            if (enclosingMethodBodyOperation != null)
            {
                // Property getter / setter with block and attribute on property
                if (enclosingMethodBodyOperation.Syntax is AccessorDeclarationSyntax accessorDeclaration)
                {
                    var propertyDeclarationSyntax = accessorDeclaration.Ancestors().FirstOrDefault(a => a is PropertyDeclarationSyntax);

                    var propertySymbol = semanticModel.GetDeclaredSymbol(propertyDeclarationSyntax);

                    if (ContainsAttribute(propertySymbol?.GetAttributes(), AttributeName))
                    {
                        return NoHiddenAllocationsLevel.Default;
                    }
                }

                var symbol = semanticModel.GetDeclaredSymbol(enclosingMethodBodyOperation.Syntax);

                if (symbol != null && symbol.GetContainingSymbolsAndSelf().Any(s => ContainsAttribute(s.GetAttributes(), AttributeName)))
                {
                    return NoHiddenAllocationsLevel.Default;
                }
            }
            
            // Property with arrow blocks (either automatic property with default or arrow based getter / setter) with attribute on property
            var enclosingArrowBlock =
                operation?.AncestorAndSelf().FirstOrDefault(o =>(o is IBlockOperation && o.Syntax is ArrowExpressionClauseSyntax));

            if (enclosingArrowBlock != null)
            {
                // Need to get a property declaration in this case for Arrow-based property
                var symbol = semanticModel.GetDeclaredSymbol(GetPropertyDeclarationSyntax((ArrowExpressionClauseSyntax) enclosingArrowBlock.Syntax));

                if (symbol != null && symbol.GetContainingSymbolsAndSelf().Any(s => ContainsAttribute(s.GetAttributes(), AttributeName)))
                {
                    return NoHiddenAllocationsLevel.Default;
                }
            }

            return null;
        }

        private static SyntaxNode GetPropertyDeclarationSyntax(ArrowExpressionClauseSyntax node)
        {
            return node.AncestorsAndSelf().FirstOrDefault(a => a is PropertyDeclarationSyntax);
        }

        public static NoHiddenAllocationsLevel? TryGetConfiguration(IOperation operation)
        {

            return TryGetConfiguration(operation.Syntax, operation.SemanticModel);
        }

        private static bool ContainsAttribute(ImmutableArray<AttributeData>? attributes, string attributeName)
        {
            return attributes.HasValue && attributes.Value.Any(a => a.AttributeClass.Name.StartsWith(attributeName));
        }

        private static IEnumerable<IOperation> AncestorAndSelf(this IOperation operation)
        {
            while (operation != null)
            {
                yield return operation;
                operation = operation.Parent;
            }
        }

        private static IEnumerable<ISymbol> GetContainingSymbolsAndSelf(this ISymbol symbol)
        {
            while (symbol != null)
            {
                yield return symbol;
                symbol = symbol.ContainingSymbol;
            }

        }

    }

    public static class ConfigureAwaitConfiguration
    {
        public static ConfigureAwait? TryGetConfigureAwait(Compilation compilation)
        {
            var attributes = compilation.Assembly.GetAttributes();
            if (attributes.Any(a => a.AttributeClass.Name.StartsWith("DoNotUseConfigureAwait")))
            {
                return ConfigureAwait.DoNotUseConfigureAwait;
            }

            if (attributes.Any(a => a.AttributeClass.Name.StartsWith("UseConfigureAwaitFalse")))
            {
                return ConfigureAwait.UseConfigureAwaitFalse;
            }


            return null;
        }
    }

    public enum ConfigureAwait
    {
        UseConfigureAwaitFalse,
        DoNotUseConfigureAwait,
    }

    internal static class TempExtensions
    {
        public static bool IsConfigureAwait(this IMethodSymbol method, Compilation compilation)
        {
            // Naive implementation
            return method.Name == "ConfigureAwait" && method.ReceiverType.IsTaskLike(compilation);
        }
    }
}
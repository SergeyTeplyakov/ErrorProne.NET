using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

[assembly:NoHiddenAllocations] public sealed class NoHiddenAllocationsAttribute : System.Attribute
{

}
namespace ErrorProne.NET.AsyncAnalyzers
{
    public static class NoHiddenAllocationsConfiguration
    {
        public enum NoHiddenAllocationsLevel
        {
            Default,
            Recursive,
        }

        private static string AttributeName = "NoHiddenAllocations";

        public static bool ShouldNotDetectAllocationsFor(SyntaxNode node, SemanticModel semanticModel)
        {
            return TryGetConfiguration(node, semanticModel) == null;
        }

        public static bool ShouldNotDetectAllocationsFor(IOperation operation)
        {
            return TryGetConfiguration(operation) == null;
        }

        private static NoHiddenAllocationsLevel? TryGetConfiguration(IOperation operation)
        {
            return TryGetConfiguration(operation.Syntax, operation.SemanticModel);
        }

        public static bool IsHiddenAllocationsAllowed(this OperationAnalysisContext context)
        {
            return TryGetConfiguration(context.Operation.Syntax, context.Operation.SemanticModel) == null;
        }

        public static NoHiddenAllocationsLevel? TryGetConfiguration(SyntaxNode node, SemanticModel semanticModel)
        {
            // The assembly can have the attribute, or any of the node's ancestors
            if (TryGetAllocationLevel(semanticModel.Compilation.Assembly.GetAttributes(), AttributeName, out var allocationLevel))
            {
                return allocationLevel;
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

                    if (TryGetAllocationLevel(propertySymbol?.GetAttributes(), AttributeName, out allocationLevel))
                    {
                        return allocationLevel;
                    }
                }

                var symbol = semanticModel.GetDeclaredSymbol(enclosingMethodBodyOperation.Syntax);

                if (symbol != null && TryGetAllocationLevelFromSymbolOrAncestors(symbol, out allocationLevel))
                {
                    return allocationLevel;
                }
            }

            // Property with arrow blocks (either automatic property with default or arrow based getter / setter) with attribute on property
            var enclosingArrowBlock =
                operation?.AncestorAndSelf().FirstOrDefault(o => o is IBlockOperation && o.Syntax is ArrowExpressionClauseSyntax);

            if (enclosingArrowBlock != null)
            {
                // Need to get a property declaration in this case for Arrow-based property
                var symbol = semanticModel.GetDeclaredSymbol(GetPropertyDeclarationSyntax((ArrowExpressionClauseSyntax) enclosingArrowBlock.Syntax));

                if (symbol != null && TryGetAllocationLevelFromSymbolOrAncestors(symbol, out allocationLevel))
                {
                    return allocationLevel;
                }
            }

            return null;
        }

        private static bool TryGetAllocationLevelFromSymbolOrAncestors(ISymbol symbol, out NoHiddenAllocationsLevel? allocationLevel)
        {
            foreach (var containingSymbol in symbol.GetContainingSymbolsAndSelf())
            {
                if (TryGetAllocationLevel(containingSymbol.GetAttributes(), AttributeName, out var level))
                {
                    allocationLevel = level;
                    return true;
                }
            }

            allocationLevel = null;
            return false;
        }

        private static SyntaxNode GetPropertyDeclarationSyntax(ArrowExpressionClauseSyntax node)
        {
            return node.AncestorsAndSelf().FirstOrDefault(a => a is PropertyDeclarationSyntax);
        }

        private static bool TryGetAllocationLevel(
            ImmutableArray<AttributeData>? attributes,
            string attributeName,
            out NoHiddenAllocationsLevel? allocationLevel)
        {
            allocationLevel = null;

            var attribute = (attributes?.Where(a => a.AttributeClass.Name.StartsWith(attributeName)) ?? Enumerable.Empty<AttributeData>()).FirstOrDefault();

            if (attribute == null)
            {
                allocationLevel = null;
                return false;
            }

            allocationLevel = attribute.NamedArguments.Any(kvp => kvp.Key.Equals("Recursive") && kvp.Value.Value.Equals(true))
                ? NoHiddenAllocationsLevel.Recursive
                : NoHiddenAllocationsLevel.Default;

            return true;
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

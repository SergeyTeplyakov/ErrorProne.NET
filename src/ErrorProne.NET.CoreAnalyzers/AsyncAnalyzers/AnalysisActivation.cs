using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.ContractsLight;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
#nullable enable

[assembly:NoHiddenAllocations] public sealed class NoHiddenAllocationsAttribute : System.Attribute
public sealed class NoHiddenAllocationsAttribute : System.Attribute
{
    public bool Recursive;
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
            Contract.Requires(node != null);
            Contract.Requires(semanticModel != null);

            return TryGetConfiguration(node, semanticModel) == null;
        }

        public static bool ShouldNotDetectAllocationsFor(IOperation operation)
        {
            Contract.Requires(operation != null);

            return TryGetConfiguration(operation) == null;
        }

        public static bool ShouldNotDetectAllocationsFor(this SyntaxNodeAnalysisContext context)
        {
            return ShouldNotDetectAllocationsFor(context.Node, context.SemanticModel);
        }

        public static bool ShouldNotDetectAllocationsFor(ISymbol symbol)
        {
            Contract.Requires(symbol != null);

            return TryGetAllocationLevelFromSymbolOrAncestors(symbol, out _) == false;
        }

        public static bool ShouldNotEnforceRecursiveApplication(IOperation operation)
        {
            Contract.Requires(operation != null);

            return TryGetConfiguration(operation) != NoHiddenAllocationsLevel.Recursive;
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
                    if (propertyDeclarationSyntax != null)
                    {
                        var propertySymbol = semanticModel.GetDeclaredSymbol(propertyDeclarationSyntax);

                        if (TryGetAllocationLevel(propertySymbol?.GetAttributes(), AttributeName, out allocationLevel))
                        {
                            return allocationLevel;
                        }
                    }
                }

                var symbol = semanticModel.GetDeclaredSymbol(enclosingMethodBodyOperation.Syntax);

                if (TryGetAllocationLevelFromSymbolOrAncestors(symbol, out allocationLevel))
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

                if (TryGetAllocationLevelFromSymbolOrAncestors(symbol, out allocationLevel))
                {
                    return allocationLevel;
                }
            }

            // node could be a property declaration syntax, which does not have operations
            if (operation == null && node is PropertyDeclarationSyntax propertySyntax)
            {
                var symbol = semanticModel.GetDeclaredSymbol(propertySyntax);

                if (TryGetAllocationLevelFromSymbolOrAncestors(symbol, out allocationLevel))
                {
                    return allocationLevel;
                }
            }

            return null;
        }

        private static bool TryGetAllocationLevelFromSymbolOrAncestors(ISymbol symbol, out NoHiddenAllocationsLevel? allocationLevel)
        {
            allocationLevel = null;

            if (symbol == null)
            {
                return false;
            }

            foreach (var containingSymbol in symbol.GetContainingSymbolsAndSelf())
            {
                if (TryGetAllocationLevel(containingSymbol.GetAttributes(), AttributeName, out var level))
                {
                    allocationLevel = level;
                    return true;
                }
            }

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

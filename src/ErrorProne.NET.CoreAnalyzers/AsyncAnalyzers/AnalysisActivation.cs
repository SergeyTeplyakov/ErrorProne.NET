using System;
using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;

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

        public static NoHiddenAllocationsLevel? TryGetConfiguration(SyntaxNode node, SemanticModel semanticModel)
        {
            // The assembly can have the attribute, or any of the node's ancestors
            if (ContainsAttribute(semanticModel.Compilation.Assembly.GetAttributes(), AttributeName) ||
                node.AncestorsAndSelf().Any(ancestor => ContainsAttribute(semanticModel.GetSymbolInfo(ancestor).Symbol?.GetAttributes(), AttributeName)))
            {
                return NoHiddenAllocationsLevel.Default;
            }

            return null;
        }

        private static bool ContainsAttribute(ImmutableArray<AttributeData>? attributes, string attributeName)
        {
            return attributes.HasValue && attributes.Value.Any(a => a.AttributeClass.Name.StartsWith(attributeName));
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
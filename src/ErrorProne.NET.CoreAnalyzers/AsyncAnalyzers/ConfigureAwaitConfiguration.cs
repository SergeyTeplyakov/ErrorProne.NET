using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.AsyncAnalyzers
{
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
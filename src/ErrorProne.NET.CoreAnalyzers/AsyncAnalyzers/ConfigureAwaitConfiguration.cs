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
            if (attributes.Any(a => a.AttributeClass?.Name.StartsWith("DoNotUseConfigureAwait") == true))
            {
                return ConfigureAwait.DoNotUseConfigureAwait;
            }

            if (attributes.Any(a => a.AttributeClass?.Name.StartsWith("UseConfigureAwaitFalse") == true))
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
}
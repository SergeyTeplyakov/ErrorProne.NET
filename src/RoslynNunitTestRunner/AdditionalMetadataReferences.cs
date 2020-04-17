using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Testing;

namespace ErrorProne.NET.TestHelpers
{
    internal static class AdditionalMetadataReferences
    {
        public static ReferenceAssemblies ReferenceAssemblies { get; } = ReferenceAssemblies.Default
            .AddPackages(ImmutableArray.Create(
                new PackageIdentity("System.Collections.Immutable", "1.7.0"),
                new PackageIdentity("System.Threading.Tasks.Extensions", "4.5.4")));
    }
}
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Core
{
    /// <summary>
    /// A helper that provide 
    /// </summary>
    public class WellKnownTypeProvider
    {
        private static readonly ConditionalWeakTable<Compilation, Lazy<WellKnownTypeProvider>> Cache = new ();
        
        // Full name to symbol map.
        private readonly ConcurrentDictionary<string, INamedTypeSymbol?> _fullNameToTypeMap;

        private WellKnownTypeProvider(Compilation compilation)
        {
            Compilation = compilation;
            _fullNameToTypeMap = new ConcurrentDictionary<string, INamedTypeSymbol?>(StringComparer.Ordinal);
        }

        public static WellKnownTypeProvider GetOrCreate(Compilation compilation)
        {
            // A delegate provided to 'GetValue' can be called twice.
            // To avoid creating an WellKnownTypeProvider more than once
            // using a trick with Lazy.
            // In this case two instances of Lazy<WellKnownTypeProvider> may be created
            // but only one of them will be observed by the caller.
            var cachedValue = Cache.GetValue(compilation,
                static compilation => new Lazy<WellKnownTypeProvider>(() => new WellKnownTypeProvider(compilation)));
            return cachedValue.Value;
        }

        public Compilation Compilation { get; }

        public INamedTypeSymbol? GetTypeByFullName(string fullName)
        {
            return _fullNameToTypeMap.GetOrAdd(
                fullName,
                v => Compilation.GetBestTypeByMetadataName(v));
        }
    }
}
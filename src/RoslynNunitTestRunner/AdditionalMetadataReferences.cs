using Microsoft.CodeAnalysis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace ErrorProne.NET.TestHelpers
{
    internal static class AdditionalMetadataReferences
    {
        private static readonly Lazy<MetadataReference> LazySystemCollections = new Lazy<MetadataReference>(
            () => MetadataReference.CreateFromFile(typeof(HashSet<>).GetTypeInfo().Assembly.Location));
        private static readonly Lazy<MetadataReference> LazySystemCollectionsConcurrent = new Lazy<MetadataReference>(
            () => MetadataReference.CreateFromFile(typeof(ConcurrentDictionary<,>).GetTypeInfo().Assembly.Location));
        private static readonly Lazy<MetadataReference> LazySystemConsole = new Lazy<MetadataReference>(
            () => MetadataReference.CreateFromFile(typeof(Console).GetTypeInfo().Assembly.Location));
        private static readonly Lazy<MetadataReference> LazySystemRuntime = new Lazy<MetadataReference>(
            () => MetadataReference.CreateFromFile(Assembly.Load(new AssemblyName("System.Runtime, Version=4.2.1.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")).Location));

        public static MetadataReference SystemCollections => LazySystemCollections.Value;
        public static MetadataReference SystemCollectionsConcurrent => LazySystemCollectionsConcurrent.Value;
        public static MetadataReference SystemConsole => LazySystemConsole.Value;
        public static MetadataReference SystemRuntime => LazySystemRuntime.Value;
    }
}
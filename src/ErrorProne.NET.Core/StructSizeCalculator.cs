using Microsoft.CodeAnalysis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.ContractsLight;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ErrorProne.NET.Core
{
    public static class StructSizeCalculator
    {
        private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<ITypeSymbol, int>> TypeSizeCache =
            new ConditionalWeakTable<Compilation, ConcurrentDictionary<ITypeSymbol, int>>();
        
        private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<ITypeSymbol, int?>> StructLayoutSizeCache =
            new ConditionalWeakTable<Compilation, ConcurrentDictionary<ITypeSymbol, int?>>();

        /// <summary>
        /// Computes size of the struct.
        /// </summary>
        /// <remarks>
        /// This computation is not perfect, but could be good for the first time.
        /// Current algorithm was reversed engineered for sequential layout by set of test cases and here it is:
        /// CLR tries to pack members based on their size.
        /// If next item has larger size, then previous items are aligned to it (with empty space)
        /// and new item is aligned in memory:
        /// [1][1] // byte, byte => 2
        /// [1   ][  2 ] // byte, short => 4
        /// [1   ][  2 ][    4    ] // byte, short, int => 8
        /// [1                    ][8                      ] // byte, long => 16
        /// [     4    ][1][1][ 2 ] // int, byte, byte, short => 8
        /// 
        /// One caveat for composite fields:
        /// For nested composite field the same rule applied recursively:
        /// 
        /// </remarks>
        public static int ComputeStructSize(this ITypeSymbol type, Compilation compilation)
        {
            var cache = TypeSizeCache.GetOrCreateValue(compilation);
            if (cache.TryGetValue(type, out var size))
            {
                return size;
            }

            return ComputeStructSizeSlow(type, cache, compilation);
        }

        private static int ComputeStructSizeSlow(ITypeSymbol type, ConcurrentDictionary<ITypeSymbol, int> cache, Compilation compilation)
        {
            // TODO: if a struct has a reference type in it, the algorithm is different!
            Contract.Requires(type != null);
            Contract.Requires(type.IsValueType);

            return cache.GetOrAdd(type, t =>
            {
                int actualSize = 0;
                int capacity = 0;
                int largestFieldSize = 0;

                ComputeSize(compilation, t, ref capacity, ref largestFieldSize, ref actualSize);

                return capacity;
            });
        }

        private static int? TryGetStructLayoutSize(ITypeSymbol type, Compilation compilation)
        {
            var cache = StructLayoutSizeCache.GetOrCreateValue(compilation);
            if (cache.TryGetValue(type, out var result))
            {
                return result;
            }

            return cache.GetOrAdd(type, t =>
            {
                return tryGetStructLayoutSize(t, compilation);
            });

            static int? tryGetStructLayoutSize(ITypeSymbol type, Compilation compilation)
            {
                var structLayoutAttribute = type.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass.Name == nameof(StructLayoutAttribute));
                if (structLayoutAttribute == null)
                {
                    // When a struct is referenced via ref assemblies, the attributes are missing on the type.
                    // Using reflection-based hack to obtain the size from the type.UnderlyingSymbol.Layout.Size
                    return RoslynLayoutSizeRetriever.TryGetUnderlyingSymbolLayoutSize(type, compilation);
                }

                var size = structLayoutAttribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Size");
                if (size.Key == null)
                {
                    return null;
                }

                return Convert.ToInt32(size.Value.Value, null);
            }
        }
        
        private static bool TryGetPrimitiveSize(Compilation compilation, ITypeSymbol type, out int size, ref int largestFieldSize)
        {
            if (type == null)
            {
                size = 0;
                return false;
            }

            if (type.IsReferenceType
                || type.TypeKind == TypeKind.Pointer
                || type.SpecialType == SpecialType.System_IntPtr
                || type.SpecialType == SpecialType.System_UIntPtr)
            {
                switch (compilation.Options.Platform)
                {
                    case Platform.AnyCpu32BitPreferred:
                    case Platform.X86:
                    case Platform.Arm:
                        size = 4;
                        break;

                    case Platform.AnyCpu:
                    case Platform.X64:
                    case Platform.Arm64:
                    case Platform.Itanium:
                        size = 8;
                        break;

                    default:
                        size = 8;
                        break;
                }
            }
            else if (type.IsEnum())
            {
                var enumType = type.GetEnumUnderlyingType();
                if (enumType != null)
                {
                    return TryGetPrimitiveSize(compilation, enumType, out size, ref largestFieldSize);
                }

                // Not sure what to do in this case!
                size = sizeof(int);
            }
            else
            {
                type.TryGetPrimitiveSize(out size);
            }

            int largestSizeCandidate = size;

            // Decimal is special beast:
            // It is not a primitive type and actually has few fields of type int,
            // that's why bucket size should be equal to sizeof(int).
            // Unfortunately, there is no way to get those fields via type instance of Decimal type.
            if (type?.SpecialType == SpecialType.System_Decimal)
            {
                largestSizeCandidate = sizeof(int);
            }

            // Bucket size should be changed only when current item's size is greater than
            // current current bucket.
            if (size > largestFieldSize || largestFieldSize == 0)
            {
                largestFieldSize = largestSizeCandidate;
            }

            return size != 0;
        }

        private static void ComputeSize(Compilation compilation, ITypeSymbol type, ref int capacity, ref int largestFieldSize, ref int actualSize)
        {
            if (type == null)
            {
                return;
            }

            int newLargestFieldSize = largestFieldSize;
            if (TryGetPrimitiveSize(compilation, type, out var currentItemSize, ref newLargestFieldSize))
            {
                if (currentItemSize > largestFieldSize)
                {
                    // we're trying to add large item. For instance, in case: byte b; int i;
                    // In this case we need to adjust current size and capacity to larger bucket
                    largestFieldSize = newLargestFieldSize;

                    int padding = (largestFieldSize - capacity % largestFieldSize)%largestFieldSize;
                    var oldCapacity = capacity;

                    capacity += padding;

                    // Removing very first case, when capacity was zero. In that case, no need to change size.
                    if (oldCapacity != 0)
                    {
                        actualSize = capacity;
                    }
                }

                if (actualSize + currentItemSize > capacity)
                {
                    // Need to take max value from it.
                    // Consider case, when the field is of a large struct type. In this case, bucket size would be IntPtr.Size
                    // but actual size would be much larger!
                    // And in this case, large struct size would be computed by this method and would be adjusted by IntPtr.Size.
                    capacity += Math.Max(currentItemSize, largestFieldSize);
                }

                actualSize += currentItemSize;
            }
            else
            {
                var fieldOrPropertyTypes = (type.IsNullableType() ? getPropertyTypesForNullableType() : getFieldOrPropertyTypes()).ToList();

                foreach (var t in fieldOrPropertyTypes)
                {
                    ComputeSize(compilation, t, ref capacity, ref largestFieldSize, ref actualSize);
                }

                if (fieldOrPropertyTypes.Count == 0)
                {
                    // Empty struct is similar to byte struct. The size is 1.
                    ComputeSize(compilation, compilation.GetSpecialType(SpecialType.System_Byte), ref capacity, ref largestFieldSize, ref actualSize);
                }

                // When composite type lay out, need to adjust actual size to current capacity,
                // because CLR will not put new fields into padding left from the composite type.
                // Consider following example:
                // struct NestedWithLongAndByteAndInt2 { struct N { byte b; long l; byte b2; } N n; int i; }
                // In this case, for N field capacity is 24, but actual size is 17.
                // But in this case, int i would not be stored in the same bucket, but new bucket would be created.
                actualSize = capacity;

                int? structLayoutSize = TryGetStructLayoutSize(type, compilation);
                if (structLayoutSize > actualSize)
                {
                    actualSize = capacity = structLayoutSize.Value;
                }

                IEnumerable<ITypeSymbol> getFieldOrPropertyTypes()
                {
                    foreach (var member in type.GetMembers().Where(f => !f.IsStatic))
                    {
                        if (member is IFieldSymbol field)
                        {
                            yield return field.Type;
                        }
                        else if (member is IPropertySymbol property && property.IsReadOnly && property.GetMethod == null)
                        {
                            yield return property.Type;
                        }
                    }
                }

                IEnumerable<ITypeSymbol> getPropertyTypesForNullableType()
                {
                    // Treating nullable types differently, because normally read-only properties should
                    // not have methods to indicate that its an auto property.
                    // But this is not the case for Nullable<T>.

                    // Just explicitly looking for 2 known properties.
                    return type.GetMembers()
                        .OfType<IPropertySymbol>()
                        .Where(p => 
                            !p.IsStatic && 
                            (p.Name == nameof(Nullable<int>.HasValue) || p.Name == nameof(Nullable<int>.Value)))
                        .Select(p => p.Type);
                        
                }
            }
        }
    }
}
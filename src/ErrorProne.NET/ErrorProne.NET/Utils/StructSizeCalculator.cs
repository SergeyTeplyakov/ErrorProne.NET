using System;
using System.Diagnostics.Contracts;
using System.Linq;
using ErrorProne.NET.Common;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Utils
{
    public static class StructSizeCalculator
    {
        /// <summary>
        /// Computes size of the struct.
        /// </summary>
        /// <remarks>
        /// This computation is not perfect, but could be good for the first time.
        /// Current algorythm was reversed engineered for sequential layout by set of test cases and here it is:
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
        public static int ComputeStructSize(this ITypeSymbol type, SemanticModel semanticModel)
        {
            Contract.Requires(type != null);
            Contract.Requires(type.IsValueType);

            // Current implementation does not respect StructLayoutAttribute.
            // It just adjusts a size based on ptr size.
            int actualSize = 0;
            int capacity = 0;
            int largestFieldSize = 0;

            GetSize(semanticModel, type, ref capacity, ref largestFieldSize, ref actualSize);
            return capacity;
        }

        private static bool TryGetPrimitiveSize(ITypeSymbol type, out int size, ref int largestFieldSize)
        {
            if (type.IsReferenceType)
            {
                size = IntPtr.Size;
            }
            else if (type.IsEnum())
            {
                var enumType = type.GetEnumUnderlyingType();
                if (enumType != null)
                {
                    return TryGetPrimitiveSize(enumType, out size, ref largestFieldSize);
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

        private static void GetSize(SemanticModel semanticModel, ITypeSymbol type, ref int capacity, ref int largestFieldSize, ref int actualSize)
        {
            int currentItemSize;

            int newLargestFieldSize = largestFieldSize;
            if (TryGetPrimitiveSize(type, out currentItemSize, ref newLargestFieldSize))
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
                bool empty = true;
                foreach (var field in type.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsStatic))
                {
                    GetSize(semanticModel, field.Type, ref capacity, ref largestFieldSize, ref actualSize);
                    empty = false;
                }

                if (empty)
                {
                    // Empty struct is similar to byte struct. The size is 1.
                    GetSize(semanticModel, semanticModel.GetClrType(typeof(byte)), ref capacity, ref largestFieldSize, ref actualSize);
                }

                // When composite type layed out, need to adjust actual size to current capacity,
                // because CLR will not put new fields into padding left from the composite type.
                // Consider following example:
                // struct NestedWithLongAndByteAndInt2 { struct N { byte b; long l; byte b2; } N n; int i; }
                // In this case, for N field capacity is 24, but actual size is 17.
                // But in this case, int i would not be stored in the same bucket, but new bucket would be created.
                actualSize = capacity;
            }
        }
    }
}
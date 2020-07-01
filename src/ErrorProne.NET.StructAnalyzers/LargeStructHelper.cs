using System.Diagnostics.CodeAnalysis;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.StructAnalyzers
{
    internal class LargeStructHelper
    {
        private readonly StructSizeCalculator _calculator;
        private readonly int _largeStructThreshold;

        public LargeStructHelper(StructSizeCalculator calculator, int largeStructThreshold)
        {
            _calculator = calculator;
            _largeStructThreshold = largeStructThreshold;
        }

        public bool IsLargeStruct([NotNullWhen(true)] ITypeSymbol? type, out int estimatedSize)
        {
            estimatedSize = 0;

            if (type == null)
            {
                return false;
            }

            if (!type.IsStruct())
            {
                return false;
            }

            estimatedSize = _calculator.ComputeStructSize(type);
            return estimatedSize >= _largeStructThreshold;
        }
    }
}
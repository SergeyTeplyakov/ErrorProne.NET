using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ErrorProne.NET.Core
{
    // TODO: rename to unsafe?
    public static class ForEachAnalysisHelper
    {
        public static IMethodSymbol GetEnumeratorMethod(this IForEachLoopOperation foreachLoop)
        {
            var loop = (BaseForEachLoopOperation)foreachLoop;

            return loop.Info.GetEnumeratorMethod;
        }

        public static ITypeSymbol GetElementType(this IForEachLoopOperation foreachLoop)
        {
            var loop = (BaseForEachLoopOperation)foreachLoop;

            return loop.Info.ElementType;
        }

        public static Conversion? GetConversionInfo(this IForEachLoopOperation foreachLoop)
        {
            var loop = (BaseForEachLoopOperation)foreachLoop;
            return loop.Info.ElementConversion as Conversion?;
        }
    }
}

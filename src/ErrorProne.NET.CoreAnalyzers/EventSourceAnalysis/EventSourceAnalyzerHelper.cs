using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.EventSourceAnalysis;

internal static class EventSourceAnalyzerHelper
{
    public static bool IsEventSourceClass(this INamedTypeSymbol namedTypeSymbol, Compilation compilation)
    {
        return namedTypeSymbol.BaseType?.IsClrType(compilation, typeof(EventSource)) == true;
    }

    private static HashSet<SpecialType> SupportedParameterTypes { get; } = new HashSet<SpecialType>
    {
        SpecialType.System_Boolean,
        SpecialType.System_Byte,
        SpecialType.System_Int16,
        SpecialType.System_Int32,
        SpecialType.System_Int64,
        SpecialType.System_SByte,
        SpecialType.System_UInt16,
        SpecialType.System_UInt32,
        SpecialType.System_UInt64,
        SpecialType.System_String,
        SpecialType.System_Char,
        SpecialType.System_DateTime,
        SpecialType.System_Single,
        SpecialType.System_Double,
        // Not supported
        // SpecialType.System_Decimal,
        SpecialType.System_IntPtr,
        SpecialType.System_UIntPtr,
    };

    public static bool IsSupportedParameterType(this ITypeSymbol typeSymbol, Compilation compilation)
    {
        // See: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/eventsource-instrumentation#supported-parameter-types
        if (SupportedParameterTypes.Contains(typeSymbol.SpecialType))
        {
            return true;
        }

        if (typeSymbol.IsClrType(compilation, typeof(Guid)) ||
            // DateTimeOffset is not supported
            // typeSymbol.IsClrType(compilation, typeof(DateTimeOffset)) ||
            typeSymbol.IsClrType(compilation, typeof(TimeSpan)))
        {
            return true;
        }

        if (typeSymbol.IsEnum())
        {
            return true;
        }

        // The doc regarding 'Supported parameter types' states that nullable types, key value pairs or serializable types
        // are supported by event sources.
        // I was able to use a struct marked with EventData attribute, but not in the event methods marked with 'Event'
        // attribute, but only in a [NonEvent] methods that call 'Write'.

        // And since this validation is happening only for the event methods, only checking the types that for sure works.

        return false;
    }
}
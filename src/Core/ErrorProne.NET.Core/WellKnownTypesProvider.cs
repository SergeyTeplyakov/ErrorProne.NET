using System;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Core
{
    public static class WellKnownTypesProvider
    {
        public static INamedTypeSymbol GetExceptionType(this SemanticModel model)
        {
            return model.Compilation.GetTypeByMetadataName(typeof(Exception).FullName);
        }

        public static INamedTypeSymbol GetBoolType(this SemanticModel model)
        {
            return model.Compilation.GetTypeByMetadataName(typeof(bool).FullName);
        }

        public static INamedTypeSymbol GetObjectType(this SemanticModel model)
        {
            return model.Compilation.GetTypeByMetadataName(typeof(object).FullName);
        }

        public static INamedTypeSymbol GetClrType(this SemanticModel model, Type type)
        {
            return model.Compilation.GetTypeByMetadataName(type.FullName);
        }
    }
}
using System;
using System.Diagnostics.Contracts;
using ErrorProne.NET.Utils;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Common
{
    public class WellKnownTypes
    {
        private readonly Lazy<INamedTypeSymbol> _exceptionType;
        private readonly Lazy<INamedTypeSymbol> _boolType;

        public WellKnownTypes(SemanticModel semanticModel)
        {
            Contract.Requires(semanticModel != null);
            var compilation = semanticModel.Compilation;

            _exceptionType = LazyEx.Create(() => compilation.GetTypeByMetadataName(typeof (Exception).FullName));
            _boolType = LazyEx.Create(() => compilation.GetTypeByMetadataName(typeof (bool).FullName));
        }

        public INamedTypeSymbol ExceptionType => _exceptionType.Value;
        public INamedTypeSymbol BoolType => _boolType.Value;
    }

    public static class WellKnownTypesProvider
    {
        public static INamedTypeSymbol GetExceptionType(this SemanticModel model)
        {
            return model.Compilation.GetTypeByMetadataName(typeof (Exception).FullName);
        }

        public static INamedTypeSymbol GetBoolType(this SemanticModel model)
        {
            return model.Compilation.GetTypeByMetadataName(typeof (bool).FullName);
        }

        public static INamedTypeSymbol GetObjectType(this SemanticModel model)
        {
            return model.Compilation.GetTypeByMetadataName(typeof (object).FullName);
        }

        public static INamedTypeSymbol GetClrType(this SemanticModel model, Type type)
        {
            return model.Compilation.GetTypeByMetadataName(type.FullName);
        }
    }
}
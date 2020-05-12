using System;
using System.Diagnostics.ContractsLight;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Core
{
    // Copied from internal ICompilationExtensions class from the roslyn codebase
    public static class CompilationExtensions
    {
        public static INamedTypeSymbol GetTypeByFullName(this Compilation compilation, string fullName)
        {
            var result = compilation.GetTypeByMetadataName(fullName);
            Contract.Assert(result != null, $"Can't find type '{fullName}'.");
            return result;
        }

        public static INamedTypeSymbol TaskType(this Compilation compilation)
            => compilation.GetTypeByFullName(typeof(Task).FullName);

        public static INamedTypeSymbol TaskOfTType(this Compilation compilation)
            => compilation.GetTypeByFullName(typeof(Task<>).FullName);

        public static INamedTypeSymbol ValueTaskOfTType(this Compilation compilation)
            => compilation.GetTypeByFullName("System.Threading.Tasks.ValueTask`1");

        public static bool IsSystemObject(this INamedTypeSymbol type)
            => type.SpecialType == SpecialType.System_Object;

        public static bool IsClrType(this ISymbol type, Compilation compilation, Type clrType)
            => type is ITypeSymbol ts && ts.OriginalDefinition.Equals(compilation.GetTypeByFullName(clrType.FullName), SymbolEqualityComparer.Default);

        public static bool IsSystemValueType(this INamedTypeSymbol type, Compilation compilation)
            => type.Equals(compilation.GetTypeByFullName("System.ValueType"), SymbolEqualityComparer.Default);

        public static (INamedTypeSymbol taskType, INamedTypeSymbol taskOfTType, INamedTypeSymbol valueTaskOfTTypeOpt) GetTaskTypes(Compilation compilation)
        {
            var taskType = compilation.TaskType();
            var taskOfTType = compilation.TaskOfTType();
            var valueTaskOfTType = compilation.ValueTaskOfTType();

            return (taskType, taskOfTType, valueTaskOfTType);
        }

        public static bool IsTaskLike(this ITypeSymbol? returnType, Compilation compilation)
        {
            if (returnType == null)
            {
                return false;
            }

            var (taskType, taskOfTType, valueTaskOfTType) = GetTaskTypes(compilation);
            if (taskType == null || taskOfTType == null)
            {
                return false; // ?
            }

            if (returnType.Equals(taskType, SymbolEqualityComparer.Default))
            {
                return true;
            }

            if (returnType.OriginalDefinition.Equals(taskOfTType, SymbolEqualityComparer.Default))
            {
                return true;
            }

            if (returnType.OriginalDefinition.Equals(valueTaskOfTType, SymbolEqualityComparer.Default))
            {
                return true;
            }

            if (returnType.IsErrorType())
            {
                return returnType.Name.Equals("Task") ||
                       returnType.Name.Equals("ValueTask");
            }

            return false;
        }

        public static bool IsErrorType(this ITypeSymbol symbol)
        {
            return symbol?.TypeKind == TypeKind.Error;
        }
    }
}
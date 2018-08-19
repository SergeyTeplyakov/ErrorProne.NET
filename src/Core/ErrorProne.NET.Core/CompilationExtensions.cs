using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Core
{
    // Copied from internal ICompilationExtensions class from the roslyn codebase
    public static class CompilationExtensions
    {
        public static INamedTypeSymbol TaskType(this Compilation compilation)
            => compilation.GetTypeByMetadataName(typeof(Task).FullName);

        public static INamedTypeSymbol TaskOfTType(this Compilation compilation)
            => compilation.GetTypeByMetadataName(typeof(Task<>).FullName);

        public static INamedTypeSymbol ValueTaskOfTType(this Compilation compilation)
            => compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");

        public static bool IsSystemObject(this INamedTypeSymbol type, Compilation compilation)
            => type.Equals(compilation.GetTypeByMetadataName("System.Object"));

        public static bool IsSystemValueType(this INamedTypeSymbol type, Compilation compilation)
            => type.Equals(compilation.GetTypeByMetadataName("System.ValueType"));

        public static (INamedTypeSymbol taskType, INamedTypeSymbol taskOfTType, INamedTypeSymbol valueTaskOfTTypeOpt) GetTaskTypes(Compilation compilation)
        {
            var taskType = compilation.TaskType();
            var taskOfTType = compilation.TaskOfTType();
            var valueTaskOfTType = compilation.ValueTaskOfTType();

            return (taskType, taskOfTType, valueTaskOfTType);
        }

        public static bool IsTaskLike(this ITypeSymbol returnType, Compilation compilation)
        {
            var (taskType, taskOfTType, valueTaskOfTType) = GetTaskTypes(compilation);
            if (taskType == null || taskOfTType == null)
            {
                return false; // ?
            }

            if (returnType.Equals(taskType))
            {
                return true;
            }

            if (returnType.OriginalDefinition.Equals(taskOfTType))
            {
                return true;
            }

            if (returnType.OriginalDefinition.Equals(valueTaskOfTType))
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
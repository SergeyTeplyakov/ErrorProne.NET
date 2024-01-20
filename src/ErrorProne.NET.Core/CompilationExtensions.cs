using System;
using System.Diagnostics.ContractsLight;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.Core
{
    public static class OperationExtensions
    {
        public static ITypeSymbol? GetReceiverType(this IInvocationOperation invocationOperation, bool includeLocal = false)
        {
            // We have (at least) two cases here:
            // instance.ToList() and
            // Enumerable.ToList(instance).
            if (invocationOperation.Arguments.Length == 0 || invocationOperation.SemanticModel is null)
            {
                return null;
            }

            var firstArg = invocationOperation.Arguments[0];
            var semanticModel = invocationOperation.SemanticModel;
            var argumentOperation = semanticModel.GetOperation(firstArg.Syntax);

            if (argumentOperation is IArgumentOperation)
            {
                // This is the same argument operation that we obtained before.
                // It means that this is a real argument like Enumerable.ToList(arg)
                // and not something like arg.ToList();

                if (firstArg.Syntax.ChildNodes().FirstOrDefault() is not IdentifierNameSyntax argumentIdentifier)
                {
                    // TODO: is it actually possible?
                    return null;
                }

                return semanticModel.GetTypeInfo(argumentIdentifier).Type;
            }

            // This is 'foo.ToList()' case.
            // Just getting a type of an operation but we need to exclude locals.

            if (argumentOperation is ILocalReferenceOperation ao)
            {
                if (includeLocal)
                {
                    return ao.Type;
                }

                // Important NOTE: excluding local variables, because it is way likely that the local is used in a shared context.
                // In most cases the locals are used in some kind of fork-join scenario, when the local is created,
                // populated in parallel via Parallel.For or something similar and then processed after that.
                // It is still possible that this code may cause issues, but because this pattern is relatively popular
                // its better to avoid false positives here.
                return null;
            }

            return argumentOperation?.Type;
        }
    }

    public sealed class TaskTypesInfo
    {
        public INamedTypeSymbol? TaskSymbol { get; }
        public INamedTypeSymbol? TaskOfTSymbol { get; }
        public INamedTypeSymbol? ValueTaskOfTSymbol { get; }

        public TaskTypesInfo(Compilation compilation)
        {
            TaskSymbol = compilation.GetTypeByMetadataName(typeof(Task).FullName);
            TaskOfTSymbol = compilation.GetTypeByMetadataName(typeof(Task<>).FullName);
            ValueTaskOfTSymbol = compilation.GetTypeByMetadataName(typeof(ValueTask<>).FullName);
        }
    }

    // Copied from internal ICompilationExtensions class from the roslyn codebase
    public static class CompilationExtensions
    {
        public static INamedTypeSymbol? GetTypeByFullName(this Compilation compilation, string? fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return null;
            }
            
            var provider = WellKnownTypeProvider.GetOrCreate(compilation);
            return provider.GetTypeByFullName(fullName!);
        }
        
        
        public static INamedTypeSymbol? GetTypeByClrType(this Compilation compilation, Type type)
        {
            return compilation.GetTypeByFullName(type.FullName);
        }
        
        public static INamedTypeSymbol? TaskType(this Compilation compilation)
            => compilation.GetTypeByFullName(typeof(Task).FullName);

        public static INamedTypeSymbol? TaskOfTType(this Compilation compilation)
            => compilation.GetTypeByFullName(typeof(Task<>).FullName);

        public static INamedTypeSymbol? ValueTaskOfTType(this Compilation compilation)
            => compilation.GetTypeByFullName("System.Threading.Tasks.ValueTask`1");

        public static bool IsSystemObject(this INamedTypeSymbol type)
            => type.SpecialType == SpecialType.System_Object;

        public static bool IsClrType(this ISymbol type, Compilation compilation, Type clrType)
            => type is ITypeSymbol ts && ts.OriginalDefinition.Equals(compilation.GetTypeByFullName(clrType.FullName), SymbolEqualityComparer.Default);
        
        public static bool IsClrType(this ITypeSymbol? ts, Compilation compilation, Type clrType)
            => ts?.OriginalDefinition.Equals(compilation.GetTypeByFullName(clrType.FullName), SymbolEqualityComparer.Default) == true;

        public static bool IsGenericType(this ITypeSymbol? type, Compilation compilation, Type clrType)
        {
            Contract.Requires(clrType.IsGenericType);
            
            var otherType = compilation.GetTypeByClrType(clrType);
            if (type is INamedTypeSymbol nts)
            {
                return nts.ConstructedFrom.Equals(otherType, SymbolEqualityComparer.Default);
            }

            return false;
        }

        public static bool IsSystemValueType(this INamedTypeSymbol type, Compilation compilation)
            => type.Equals(compilation.GetTypeByFullName("System.ValueType"), SymbolEqualityComparer.Default);

        public static (INamedTypeSymbol? taskType, INamedTypeSymbol? taskOfTType, INamedTypeSymbol? valueTaskOfTTypeOpt) GetTaskTypes(Compilation compilation)
        {
            var taskType = compilation.TaskType();
            var taskOfTType = compilation.TaskOfTType();
            var valueTaskOfTType = compilation.ValueTaskOfTType();

            return (taskType, taskOfTType, valueTaskOfTType);
        }

        public static bool IsTaskLike(this ITypeSymbol? returnType, TaskTypesInfo info)
        {
            if (returnType == null)
            {
                return false;
            }

            if (info.TaskSymbol == null || info.TaskOfTSymbol == null)
            {
                return false; // ?
            }

            if (returnType.Equals(info.TaskSymbol, SymbolEqualityComparer.Default))
            {
                return true;
            }

            if (returnType.OriginalDefinition.Equals(info.TaskOfTSymbol, SymbolEqualityComparer.Default))
            {
                return true;
            }

            if (returnType.OriginalDefinition.Equals(info.ValueTaskOfTSymbol, SymbolEqualityComparer.Default))
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

        /// <summary>
        /// Gets a type by its metadata name to use for code analysis within a <see cref="Compilation"/>. This method
        /// attempts to find the "best" symbol to use for code analysis, which is the symbol matching the first of the
        /// following rules.
        ///
        /// <list type="number">
        ///   <item><description>
        ///     If only one type with the given name is found within the compilation and its referenced assemblies, that
        ///     type is returned regardless of accessibility.
        ///   </description></item>
        ///   <item><description>
        ///     If the current <paramref name="compilation"/> defines the symbol, that symbol is returned.
        ///   </description></item>
        ///   <item><description>
        ///     If exactly one referenced assembly defines the symbol in a manner that makes it visible to the current
        ///     <paramref name="compilation"/>, that symbol is returned.
        ///   </description></item>
        ///   <item><description>
        ///     Otherwise, this method returns <see langword="null"/>.
        ///   </description></item>
        /// </list>
        /// </summary>
        /// <param name="compilation">The <see cref="Compilation"/> to consider for analysis.</param>
        /// <param name="fullyQualifiedMetadataName">The fully-qualified metadata type name to find.</param>
        /// <returns>The symbol to use for code analysis; otherwise, <see langword="null"/>.</returns>
        /// <remarks>
        /// This code is copied from github.com/dotnet/roslyn/blob/master/src/Workspaces/SharedUtilitiesAndExtensions/Compiler/Core/Extensions/CompilationExtensions.cs
        /// </remarks>
        internal static INamedTypeSymbol? GetBestTypeByMetadataName(this Compilation compilation, string fullyQualifiedMetadataName)
        {
            // Try to get the unique type with this name, ignoring accessibility
            var type = compilation.GetTypeByMetadataName(fullyQualifiedMetadataName);

            // Otherwise, try to get the unique type with this name originally defined in 'compilation'
            type ??= compilation.Assembly.GetTypeByMetadataName(fullyQualifiedMetadataName);

            // Otherwise, try to get the unique accessible type with this name from a reference
            if (type is null)
            {
                foreach (var module in compilation.Assembly.Modules)
                {
                    foreach (var referencedAssembly in module.ReferencedAssemblySymbols)
                    {
                        var currentType = referencedAssembly.GetTypeByMetadataName(fullyQualifiedMetadataName);
                        if (currentType is null)
                        {
                            continue;
                        }

                        switch (currentType.GetResultantVisibility())
                        {
                            case SymbolVisibility.Public:
                            case SymbolVisibility.Internal when referencedAssembly.GivesAccessTo(compilation.Assembly):
                                break;

                            default:
                                continue;
                        }

                        if (type is not null)
                        {
                            // Multiple visible types with the same metadata name are present
                            return null;
                        }

                        type = currentType;
                    }
                }
            }

            return type;
        }
    }
}
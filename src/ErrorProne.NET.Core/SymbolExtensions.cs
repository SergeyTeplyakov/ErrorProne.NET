using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.Core
{
    public enum SymbolVisibility
    {
        Public,
        Internal,
        Private,
    }

    /// <nodoc />
    public static class SymbolExtensions
    {
        public static bool HasAttributeWithName(this ISymbol? symbol, string attributeName)
        {
            return symbol?.GetAttributes().Any(a => a.AttributeClass.Name == attributeName) == true;
        }

        public static bool IsConstructor(this ISymbol symbol)
        {
            return (symbol is IMethodSymbol methodSymbol && methodSymbol.MethodKind == MethodKind.Constructor);
        }

        public static bool IsDisposeMethod(this ISymbol symbol)
        {
            if (symbol is IMethodSymbol method
                && method.Name == nameof(IDisposable.Dispose)
                && method.ContainingType.Interfaces.Any(i => i.Name == nameof(IDisposable)))
            {
                return true;
            }

            return false;
        }

        public static SymbolVisibility GetResultantVisibility(this ISymbol symbol)
        {
            // Start by assuming it's visible.
            var visibility = SymbolVisibility.Public;

            switch (symbol.Kind)
            {
                case SymbolKind.Alias:
                    // Aliases are uber private.  They're only visible in the same file that they
                    // were declared in.
                    return SymbolVisibility.Private;

                case SymbolKind.Parameter:
                    // Parameters are only as visible as their containing symbol
                    return GetResultantVisibility(symbol.ContainingSymbol);

                case SymbolKind.TypeParameter:
                    // Type Parameters are private.
                    return SymbolVisibility.Private;
            }

            while (symbol != null && symbol.Kind != SymbolKind.Namespace)
            {
                switch (symbol.DeclaredAccessibility)
                {
                    // If we see anything private, then the symbol is private.
                    case Accessibility.NotApplicable:
                    case Accessibility.Private:
                        return SymbolVisibility.Private;

                    // If we see anything internal, then knock it down from public to
                    // internal.
                    case Accessibility.Internal:
                    case Accessibility.ProtectedAndInternal:
                        visibility = SymbolVisibility.Internal;
                        break;

                    // For anything else (Public, Protected, ProtectedOrInternal), the
                    // symbol stays at the level we've gotten so far.
                }

                symbol = symbol.ContainingSymbol;
            }

            return visibility;
        }

        public static IEnumerable<ISymbol> GetAllUsedSymbols(Compilation compilation, SyntaxNode root)
        {
            var noDuplicates = new HashSet<ISymbol>();

            var model = compilation.GetSemanticModel(root.SyntaxTree);

            foreach (var node in root.DescendantNodesAndSelf())
            {
                switch (node.Kind())
                {
                    case SyntaxKind.ExpressionStatement:
                    case SyntaxKind.InvocationExpression:
                        break;
                    default:
                        ISymbol? symbol = model.GetSymbolInfo(node).Symbol;

                        if (symbol != null)
                        {
                            if (noDuplicates.Add(symbol))
                            {
                                yield return symbol;
                            }
                        }
                        break;
                }
            }
        }

        public static bool TryGetMethodSyntax(this IMethodSymbol method, [NotNullWhen(true)]out MethodDeclarationSyntax? result)
        {
            result = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax;
            return result != null;
        }

        /// <summary>
        /// Returns location of a given parameter.
        /// </summary>
        public static Location GetParameterLocation(this IParameterSymbol parameter)
        {
            // When the code is partially correct (for instance, when the name is missing), then
            // DeclaringSyntaxReferences is empty.
            if (parameter.DeclaringSyntaxReferences.Length != 0)
            {
                // Can't just use p.Location, because it will capture just a span for parameter name.
                var span = parameter.DeclaringSyntaxReferences[0].GetSyntax().Span;
                return Location.Create(parameter.DeclaringSyntaxReferences[0].SyntaxTree, span);
            }

            return parameter.Locations[0];
        }

        /// <summary>
        /// Returns true if a given <paramref name="method"/> is <see cref="Task.ConfigureAwait(bool)"/>.
        /// </summary>
        public static bool IsConfigureAwait(this IMethodSymbol method, Compilation compilation)
        {
            // Naive implementation
            return method.Name == "ConfigureAwait" && method.ReceiverType.IsTaskLike(compilation);
        }

        /// <summary>
        /// Returns true if a given <paramref name="method"/> is <see cref="Task.ContinueWith(System.Action{System.Threading.Tasks.Task,object},object)"/>.
        /// </summary>
        public static bool IsContinueWith(this IMethodSymbol method, Compilation compilation)
        {
            return method.Name == "ContinueWith" && method.ReceiverType.IsTaskLike(compilation) && method.ReturnType.IsTaskLike(compilation);
        }

        /// <summary>
        /// Returns true if a given <paramref name="method"/> has iterator block inside of it.
        /// </summary>
        public static bool IsIteratorBlock(this IMethodSymbol method)
        {
            Debug.Assert(method.DeclaringSyntaxReferences.Length != 0);

            return method.DeclaringSyntaxReferences
                .Select(sr => sr.GetSyntax())
                .OfType<MethodDeclarationSyntax>()
                .Any(md => md.IsIteratorBlock()) == true;
        }

        /// <summary>
        /// Returns true if the given <paramref name="method"/> is async or return task-like type.
        /// </summary>
        public static bool IsAsyncOrTaskBased(this IMethodSymbol method, Compilation compilation)
        {
            // Currently method detects only Task<T> or ValueTask<T>
            if (method.IsAsync)
            {
                return true;
            }

            return method.ReturnType.IsTaskLike(compilation);
        }

        /// <summary>
        /// Returns true if a given <paramref name="method"/> is an implementation of an interface member.
        /// </summary>
        public static bool IsInterfaceImplementation(this IMethodSymbol method)
            => IsInterfaceImplementation(method, out _);
        
        /// <summary>
        /// Returns true if a given <paramref name="method"/> is an implementation of an interface member.
        /// </summary>
        public static bool IsInterfaceImplementation(this IMethodSymbol method, [NotNullWhen(true)]out ISymbol? implementedMethod)
        {
            if (method.MethodKind == MethodKind.ExplicitInterfaceImplementation)
            {
                implementedMethod = method;
                return true;
            }

            implementedMethod = null;
            if (method.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }

            var containingType = method.ContainingType;
            var implementedInterfaces = containingType.AllInterfaces;

            foreach (var implementedInterface in implementedInterfaces)
            {
                var implementedInterfaceMembersWithSameName = implementedInterface.GetMembers(method.Name);
                foreach (var implementedInterfaceMember in implementedInterfaceMembersWithSameName)
                {
                    if (method.Equals(containingType.FindImplementationForInterfaceMember(implementedInterfaceMember), SymbolEqualityComparer.Default))
                    {
                        implementedMethod = implementedInterfaceMember;
                        return true;
                    }
                }
            }

            return false;
        }

        public static VariableDeclarationSyntax? TryGetDeclarationSyntax(this IFieldSymbol symbol)
        {
            if (symbol.DeclaringSyntaxReferences.Length == 0)
            {
                return null;
            }

            var syntaxReference = symbol.DeclaringSyntaxReferences[0];
            return syntaxReference.GetSyntax().FirstAncestorOrSelf<VariableDeclarationSyntax>();
        }

        public static PropertyDeclarationSyntax? TryGetDeclarationSyntax(this IPropertySymbol symbol)
        {
            if (symbol.DeclaringSyntaxReferences.Length == 0)
            {
                return null;
            }

            var syntaxReference = symbol.DeclaringSyntaxReferences[0];
            return syntaxReference.GetSyntax().FirstAncestorOrSelf<PropertyDeclarationSyntax>();
        }

        public static MethodDeclarationSyntax? TryGetDeclarationSyntax(this IMethodSymbol symbol)
        {
            if (symbol.DeclaringSyntaxReferences.Length == 0)
            {
                return null;
            }

            var syntaxReference = symbol.DeclaringSyntaxReferences[0];
            return syntaxReference.GetSyntax().FirstAncestorOrSelf<MethodDeclarationSyntax>();
        }

        public static bool ExceptionFromCatchBlock(this ISymbol symbol)
        {
            return
                (symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()) is CatchDeclarationSyntax;

            // There is additional interface, called ILocalSymbolInternal
            // that has IsCatch property, but, unfortunately, that interface is internal.
            // Use following code if the trick with DeclaredSyntaxReferences would not work properly!
            // return (bool?)(symbol.GetType().GetRuntimeProperty("IsCatch")?.GetValue(symbol)) == true;
        }

        public static bool IsPrivate(this ISymbol symbol)
        {
            return symbol.DeclaredAccessibility == Accessibility.Private;
        }
    }

    public static class MethodSymbolExtensions
    {
        /// <summary>
        /// Checks if the given method matches Dispose method convention and can be recognized by "using".
        /// </summary>
        public static bool HasDisposeSignatureByConvention(this IMethodSymbol method)
        {
            return method.HasDisposeMethodSignature()
                   && !method.IsStatic
                   && !method.IsPrivate();
        }

        /// <summary>
        /// Checks if the given method has the signature "void Dispose()".
        /// </summary>
        private static bool HasDisposeMethodSignature(this IMethodSymbol method)
        {
            return method.Name == "Dispose" && method.MethodKind == MethodKind.Ordinary &&
                   method.ReturnsVoid && method.Parameters.IsEmpty;
        }
    }

    // TODO: move to another file
    internal static class ControlFlowGraphExtensions
    {
        public static BasicBlock GetEntry(this ControlFlowGraph cfg) => cfg.Blocks.Single(b => b.Kind == BasicBlockKind.Entry);
        public static BasicBlock GetExit(this ControlFlowGraph cfg) => cfg.Blocks.Single(b => b.Kind == BasicBlockKind.Exit);
        public static IEnumerable<IOperation> DescendantOperations(this ControlFlowGraph cfg)
        {
            foreach (BasicBlock block in cfg.Blocks)
            {
                foreach (IOperation operation in block.DescendantOperations())
                {
                    yield return operation;
                }
            }
        }

        public static bool IsFinallyBlock(this BasicBlock block)
        {
            return block.Kind == BasicBlockKind.Block && block.EnclosingRegion.Kind == ControlFlowRegionKind.Finally;
        }
        
        public static bool IsTryBlock(this BasicBlock block)
        {
            return block.Kind == BasicBlockKind.Block && block.EnclosingRegion.Kind == ControlFlowRegionKind.Try;
        }
        
        public static bool IsCatchBlock(this BasicBlock block)
        {
            return block.Kind == BasicBlockKind.Block && block.EnclosingRegion.Kind == ControlFlowRegionKind.Catch;
        }

        public static IEnumerable<IOperation> DescendantOperations(this BasicBlock basicBlock)
        {
            foreach (var statement in basicBlock.Operations)
            {
                foreach (var operation in statement.DescendantsAndSelf())
                {
                    yield return operation;
                }
            }

            if (basicBlock.BranchValue != null)
            {
                foreach (var operation in basicBlock.BranchValue.DescendantsAndSelf())
                {
                    yield return operation;
                }
            }
        }

        public static IEnumerable<T> DescendantOperations<T>(this ControlFlowGraph cfg, OperationKind operationKind)
            where T : IOperation
        {
            foreach (var descendant in cfg.DescendantOperations())
            {
                if (descendant?.Kind == operationKind)
                {
                    yield return (T)descendant;
                }
            }
        }

        internal static bool SupportsFlowAnalysis(this ControlFlowGraph cfg)
        {
            // Skip flow analysis for following root operation blocks:
            // 1. Null root operation (error case)
            // 2. OperationKindEx.Attribute or OperationKind.None (used for attributes before IAttributeOperation support).
            // 3. OperationKind.ParameterInitialzer (default parameter values).
            if (cfg.OriginalOperation == null ||
                cfg.OriginalOperation.Kind is OperationKindEx.Attribute or OperationKind.None or OperationKind.ParameterInitializer)
            {
                return false;
            }

            // Skip flow analysis for code with syntax/semantic errors
            if (cfg.OriginalOperation.Syntax.GetDiagnostics().Any(d => d.DefaultSeverity == DiagnosticSeverity.Error) ||
                cfg.OriginalOperation.HasAnyOperationDescendant(o => o is IInvalidOperation))
            {
                return false;
            }

            return true;
        }

        public static bool HasAnyOperationDescendant(this ImmutableArray<IOperation> operationBlocks, Func<IOperation, bool> predicate)
        {
            foreach (var operationBlock in operationBlocks)
            {
                if (operationBlock.HasAnyOperationDescendant(predicate))
                {
                    return true;
                }
            }

            return false;
        }
        
        public static bool HasAnyOperationDescendant(this ImmutableArray<IOperation> operationBlocks, Func<IOperation, bool> predicate, [NotNullWhen(returnValue: true)] out IOperation? foundOperation)
        {
            foreach (var operationBlock in operationBlocks)
            {
                if (operationBlock.HasAnyOperationDescendant(predicate, out foundOperation))
                {
                    return true;
                }
            }

            foundOperation = null;
            return false;
        }

        public static bool HasAnyOperationDescendant(this ImmutableArray<IOperation> operationBlocks, OperationKind kind)
        {
            return operationBlocks.HasAnyOperationDescendant(predicate: operation => operation.Kind == kind);
        }

        public static bool HasAnyOperationDescendant(this IOperation operationBlock, Func<IOperation, bool> predicate)
        {
            return operationBlock.HasAnyOperationDescendant(predicate, out _);
        }

        public static bool HasAnyOperationDescendant(this IOperation operationBlock, Func<IOperation, bool> predicate, [NotNullWhen(returnValue: true)] out IOperation? foundOperation)
        {
            foreach (var descendant in operationBlock.DescendantsAndSelf())
            {
                if (predicate(descendant))
                {
                    foundOperation = descendant;
                    return true;
                }
            }

            foundOperation = null;
            return false;
        }
    }

    internal static class OperationKindEx
    {
        public const OperationKind FunctionPointerInvocation = (OperationKind)0x78;
        public const OperationKind ImplicitIndexerReference = (OperationKind)0x7b;
        public const OperationKind Utf8String = (OperationKind)0x7c;
        public const OperationKind Attribute = (OperationKind)0x7d;
    }
}
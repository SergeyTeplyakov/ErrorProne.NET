using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.Core
{
    /// <nodoc />
    public static class SymbolExtensions
    {
        public static bool IsConstructor(this ISymbol symbol)
        {
            return (symbol is IMethodSymbol methodSymbol && methodSymbol.MethodKind == MethodKind.Constructor);
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
                        ISymbol symbol = model.GetSymbolInfo(node).Symbol;

                        if (symbol != null)
                        {
                            if (noDuplicates.Add(symbol))
                                yield return symbol;
                        }
                        break;
                }
            }
        }

        public static bool TryGetMethodSyntax(this IMethodSymbol method, out MethodDeclarationSyntax result)
        {
            result = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax;
            return result != null;
        }

        /// <summary>
        /// Returns location of a given parameter.
        /// </summary>
        public static Location GetParametersLocation(this IParameterSymbol parameter)
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
        public static bool IsInterfaceImplementation(this IMethodSymbol method, out ISymbol implementedMethod)
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
                    if (method.Equals(containingType.FindImplementationForInterfaceMember(implementedInterfaceMember)))
                    {
                        implementedMethod = implementedInterfaceMember;
                        return true;
                    }
                }
            }

            return false;
        }

        public static VariableDeclarationSyntax TryGetDeclarationSyntax(this IFieldSymbol symbol)
        {
            if (symbol.DeclaringSyntaxReferences.Length == 0)
            {
                return null;
            }

            var syntaxReference = symbol.DeclaringSyntaxReferences[0];
            return syntaxReference.GetSyntax().FirstAncestorOrSelf<VariableDeclarationSyntax>();
        }

        public static PropertyDeclarationSyntax TryGetDeclarationSyntax(this IPropertySymbol symbol)
        {
            if (symbol.DeclaringSyntaxReferences.Length == 0)
            {
                return null;
            }

            var syntaxReference = symbol.DeclaringSyntaxReferences[0];
            return syntaxReference.GetSyntax().FirstAncestorOrSelf<PropertyDeclarationSyntax>();
        }

        public static MethodDeclarationSyntax TryGetDeclarationSyntax(this IMethodSymbol symbol)
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
    }
}
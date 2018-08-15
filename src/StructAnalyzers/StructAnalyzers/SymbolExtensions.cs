using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace ErrorProne.NET.Structs
{
    /// <nodoc />
    public static class SymbolExtensions
    {
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
        {
            if (method.MethodKind == MethodKind.ExplicitInterfaceImplementation)
            {
                return true;
            }

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
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
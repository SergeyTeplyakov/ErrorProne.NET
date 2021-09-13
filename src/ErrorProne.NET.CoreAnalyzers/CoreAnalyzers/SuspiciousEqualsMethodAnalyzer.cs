using System;
using System.Collections.Immutable;
using System.Diagnostics.ContractsLight;
using System.Linq;
using ErrorProne.NET.Core;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// Analyzer that warns about suspicious implementation of <see cref="object.Equals(object)"/> methods.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class SuspiciousEqualsMethodAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptors.EPC11;

        /// <nodoc />
        public SuspiciousEqualsMethodAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            // I didn't figure out the way to find all the symbols used in a method.
            // The only solution to find if a method references instance members or not is to use
            // a long chain of 'RegisterSomAction' methods.
            context.RegisterCompilationStartAction(context =>
            {
                context.RegisterOperationBlockStartAction(context =>
                {
                    // Only interested in 'object.Equals' and `IEquatable<T>.Equals' methods
                    if (context.OwningSymbol is IMethodSymbol ms &&
                        !OnlyThrow(ms) &&
                        (OverridesEquals(ms, context.Compilation) || ImplementsEquals(ms, context.Compilation)))
                    {
                        // Checking that Equals method accesses instance members
                        if (HasInstanceMembers(ms.ContainingType))
                        {
                            bool isInstanceReferenced = false;

                            context.RegisterOperationAction(context =>
                            {
                                isInstanceReferenced = true;
                            }, OperationKind.InstanceReference);

                            context.RegisterOperationBlockEndAction(context =>
                            {
                                if (!isInstanceReferenced)
                                {
                                    var diagnostic = Diagnostic.Create(
                                        Rule,
                                        ms.Locations[0],
                                        "Instance members are not used");

                                    context.ReportDiagnostic(diagnostic);
                                }

                                // Checking that Equals method uses 'obj' parameter
                                if (!ms.TryGetMethodSyntax(out var methodSyntax))
                                {
                                    return;
                                }

                                var bodyOrExpression = (SyntaxNode?)methodSyntax.Body ?? methodSyntax.ExpressionBody;
                                
                                Contract.Assert(bodyOrExpression != null);
                                var symbols = SymbolExtensions.GetAllUsedSymbols(context.Compilation, bodyOrExpression).ToList();
                                if (!symbols.Any(s => 
                                    s is IParameterSymbol p && p.ContainingSymbol.Equals(ms, SymbolEqualityComparer.Default) && !p.IsThis))
                                {
                                    var location = ms.Parameters[0].Locations[0];

                                    // 'obj' is not used
                                    var diagnostic = Diagnostic.Create(
                                        Rule,
                                        location,
                                        $"right hand side parameter '{ms.Parameters[0].Name}' is never used");

                                    context.ReportDiagnostic(diagnostic);
                                }
                            });
                        }
                    }
                });
            });
        }

        private static bool OverridesEquals(IMethodSymbol method, Compilation compilation)
            => method.IsOverride &&
               method.OverriddenMethod?.Name == "Equals" &&
               (method.OverriddenMethod.ContainingType.IsSystemObject() ||
                method.OverriddenMethod.ContainingType.IsSystemValueType(compilation));

        private static bool ImplementsEquals(IMethodSymbol method, Compilation compilation)
            => method.IsInterfaceImplementation(out var interfaceMethod) && interfaceMethod is IMethodSymbol ms &&
               ms.ContainingType.IsClrType(compilation, typeof(IEquatable<>));

        private static bool OnlyThrow(IMethodSymbol method)
        {
            Contract.Requires(method != null);
            return method.TryGetMethodSyntax(out var syntax) && OnlyThrow(syntax);
        }

        private static bool OnlyThrow(MethodDeclarationSyntax method)
        {
            Contract.Requires(method != null);

            if (method.Body != null)
            {
                if (method.Body.Statements.Count == 1 && method.Body.Statements[0].Kind() == SyntaxKind.ThrowStatement)
                {
                    return true;
                }
            }
            else
            {
                return method.ExpressionBody?.Expression.Kind() == SyntaxKind.ThrowExpression;
            }

            return false;
        }

        private static bool HasInstanceMembers(INamedTypeSymbol type)
        {
            // Looking for instance members but excluding constructors.
            return type.GetMembers()
                .Where(m => !m.IsStatic)
                .Any(m => m is IFieldSymbol || m is IPropertySymbol);
        }

        private static bool IsInstanceMember(INamedTypeSymbol methodContainingType, ISymbol symbol)
        {
            if (symbol.IsStatic)
            {
                return false;
            }

            if (
                symbol is IMethodSymbol ms 
                && ms.Parameters.Length == 1 
                && (ms.Name == "Equals" || ms.Name == "CompareTo") 
                && symbol.ContainingType?.Equals(methodContainingType, SymbolEqualityComparer.Default) == true)
            {
                // Special case for Equals(object) => this is MyT && Equals((MyT)other).
                return true;
            }

            if (!(
                symbol is IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol or ILocalSymbol || 
                (symbol is IParameterSymbol ps && ps.IsThis)))
            {
                // If symbol is not one of these, it is definitely not an instance member reference
                return false;
            }

            var symbolDeclaredType = symbol.ContainingType;
            if (symbolDeclaredType == null)
            {
                return false;
            }

            // Still need to check that the member belongs to this type or one of its base types.
            var typeHierarchy = methodContainingType.TraverseTypeAndItsBaseTypes().ToImmutableHashSet();
            return typeHierarchy.Contains(symbolDeclaredType);
        }
    }
}
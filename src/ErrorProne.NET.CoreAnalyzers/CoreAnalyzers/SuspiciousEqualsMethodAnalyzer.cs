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
        public const string DiagnosticId = DiagnosticIds.SuspiciousEqualsMethodImplementation;

        private const string RhsTitle = "Suspicious equality implementation: Equals method does not use rhs-parameter.";
        private const string RhsMessageFormat = "Suspicious equality implementation: parameter '{0}' is never used.";
        private const string RhsDescription = "Equals method implementation that does not uses another instance is suspicious.";

        private const string Title = "Suspicious equality implementation: no instance members are used.";
        private const string Description = "Equals method implementation that does not uses any instance members is suspicious.";
        private const string Category = "CodeSmell";

        // Using warning for visibility purposes
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        internal static readonly DiagnosticDescriptor InstanceMembersAreNotUsedRule =
            new DiagnosticDescriptor(DiagnosticId, Title, Title, Category, Severity, isEnabledByDefault: true, description: Description);

        /// <nodoc />
        internal static readonly DiagnosticDescriptor RightHandSideIsNotUsedRule =
            new DiagnosticDescriptor(DiagnosticId, RhsTitle, RhsMessageFormat, Category, Severity, isEnabledByDefault: true, description: RhsDescription);

        /// <nodoc />
        public SuspiciousEqualsMethodAnalyzer()
            : base(supportFading: true, InstanceMembersAreNotUsedRule, RightHandSideIsNotUsedRule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            // I didn't figure out the way to find all the symbols used in a method.
            // The only solution to find if a method references instance members or not is to use
            // a long chain of 'RegisterSomAction' methods.
            context.RegisterCompilationStartAction(compilationContext =>
            {
                compilationContext.RegisterOperationBlockStartAction(blockStartContext =>
                {
                    // Only interested in 'object.Equals' and `IEquatable<T>.Equals' methods
                    if (blockStartContext.OwningSymbol is IMethodSymbol ms &&
                        !OnlyThrow(ms) &&
                        (OverridesEquals(ms, blockStartContext.Compilation) || ImplementsEquals(ms, blockStartContext.Compilation)))
                    {
                        // Checking that Equals method accesses instance members
                        if (HasInstanceMembers(ms.ContainingType))
                        {
                            bool isInstanceReferenced = false;

                            blockStartContext.RegisterOperationAction(operationContext =>
                            {
                                isInstanceReferenced = true;
                            }, OperationKind.InstanceReference);

                            blockStartContext.RegisterOperationBlockEndAction(blockEndContext =>
                            {
                                if (!isInstanceReferenced)
                                {
                                    var diagnostic = Diagnostic.Create(
                                        InstanceMembersAreNotUsedRule,
                                        ms.Locations[0]);

                                    blockEndContext.ReportDiagnostic(diagnostic);
                                }

                                // Checking that Equals method uses 'obj' parameter
                                if (!ms.TryGetMethodSyntax(out var methodSyntax))
                                {
                                    return;
                                }

                                var bodyOrExpression = (SyntaxNode?)methodSyntax.Body ?? methodSyntax.ExpressionBody;
                                
                                Contract.Assert(bodyOrExpression != null);
                                var symbols = SymbolExtensions.GetAllUsedSymbols(blockStartContext.Compilation, bodyOrExpression).ToList();
                                if (!symbols.Any(s => 
                                    s is IParameterSymbol p && p.ContainingSymbol.Equals(ms, SymbolEqualityComparer.Default) && !p.IsThis))
                                {
                                    var location = ms.Parameters[0].Locations[0];

                                    // 'obj' is not used
                                    var diagnostic = Diagnostic.Create(
                                        RightHandSideIsNotUsedRule,
                                        location,
                                        ms.Parameters[0].Name);

                                    blockEndContext.ReportDiagnostic(diagnostic);

                                    // Fading 'obj' away

                                    blockEndContext.ReportDiagnostic(
                                        Diagnostic.Create(
                                            UnnecessaryWithSuggestionDescriptor!,
                                            location));
                                }
                            });
                        }
                    }
                });
            });
        }

        private static bool OverridesEquals(IMethodSymbol method, Compilation compilation)
            => method.IsOverride &&
               method.OverriddenMethod != null &&
               method.OverriddenMethod.Name == "Equals" &&
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
                symbol is IMethodSymbol ||
                symbol is IPropertySymbol ||
                symbol is IFieldSymbol ||
                symbol is IEventSymbol ||
                symbol is ILocalSymbol ||
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
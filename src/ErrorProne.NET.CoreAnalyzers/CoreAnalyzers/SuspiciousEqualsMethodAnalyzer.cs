using System;
using System.Linq;
using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Core.CoreAnalyzers
{
    /// <summary>
    /// Analyzer that warns about suspicious implementation of <see cref="object.Equals(object)"/> methods.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class SuspiciousEqualsMethodAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.SuspiciousEqualsMethodImplementation;

        private static readonly string RhsTitle = "Suspicious equality implementation: Equals method does not use rhs-parameter.";
        private static readonly string RhsMessageFormat = "Suspicious equality implementation: parameter {0} is never used.";
        private static readonly string RhsDescription = "Equals method implementation that does not uses another instance is suspicious.";

        private static readonly string Title = "Suspicious equality implementation: no instance members are used.";
        private static readonly string Description = "Equals method implementation that does not uses any instance members is suspicious.";
        private const string Category = "CodeSmell";

        // Using warning for visibility purposes
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        private static readonly DiagnosticDescriptor InstanceMembersAreNotUsedRule =
            new DiagnosticDescriptor(DiagnosticId, Title, Title, Category, Severity, isEnabledByDefault: true, description: Description);

        /// <nodoc />
        private static readonly DiagnosticDescriptor RightHandSideIsNotUsedRule =
            new DiagnosticDescriptor(DiagnosticId, RhsTitle, RhsMessageFormat, Category, Severity, isEnabledByDefault: true, description: RhsDescription);

        /// <nodoc />
        public SuspiciousEqualsMethodAnalyzer() 
            : base(InstanceMembersAreNotUsedRule, RightHandSideIsNotUsedRule)
        {
        }

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
        }

        private static bool OverridesEquals(IMethodSymbol method, Compilation compilation)
            => method.IsOverride &&
               method.OverriddenMethod != null &&
               method.OverriddenMethod.Name == "Equals" &&
               (method.OverriddenMethod.ContainingType.IsSystemObject(compilation) ||
                method.OverriddenMethod.ContainingType.IsSystemValueType(compilation));

        private static bool ImplementsEquals(IMethodSymbol method, Compilation compilation)
            => method.IsInterfaceImplementation(out var interfaceMethod) && interfaceMethod is IMethodSymbol ms &&
               ms.ContainingType.IsClrType(compilation, typeof(IEquatable<>));

        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var method = (IMethodSymbol)context.SemanticModel.GetDeclaredSymbol(context.Node);

            if (method != null)
            {
                // Method overrides System.Equals
                if (OverridesEquals(method, context.Compilation) || ImplementsEquals(method, context.Compilation))
                {
                    var methodSyntax = (MethodDeclarationSyntax)context.Node;
                    if (OnlyThrow(methodSyntax))
                    {
                        // It is ok, if Equals method just throws.
                        return;
                    }

                    var bodyOrExpression = (SyntaxNode)methodSyntax.Body ?? methodSyntax.ExpressionBody;
                    var symbols = SymbolExtensions.GetAllUsedSymbols(context.Compilation, bodyOrExpression).ToList();

                    if (HasInstanceMembers(method.ContainingType) && 
                        symbols.Count(s => IsInstanceMember(method.ContainingType, s, context.Compilation)) == 0)
                    {
                        var diagnostic = Diagnostic.Create(
                            InstanceMembersAreNotUsedRule,
                            method.Locations[0]);

                        context.ReportDiagnostic(diagnostic);
                    }

                    if (!symbols.Any(s => s is IParameterSymbol p && p.ContainingSymbol == method && !p.IsThis))
                    {
                        var location = method.Parameters[0].Locations[0];
                        // 'obj' is not used
                        var diagnostic = Diagnostic.Create(
                            RightHandSideIsNotUsedRule,
                            location,
                            method.Parameters[0].Name);

                        context.ReportDiagnostic(diagnostic);

                        // Fading 'obj' away
                        
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                UnnecessaryWithSuggestionDescriptor,
                                location));
                    }
                }
            }
        }

        private bool OnlyThrow(MethodDeclarationSyntax method)
        {
            if (method.Body != null)
            {
                if (method.Body.Statements.Count == 1 && method.Body.Statements[0].Kind() == SyntaxKind.ThrowStatement)
                {
                    return true;
                }
            }
            else
            {
                return method.ExpressionBody.Expression.Kind() == SyntaxKind.ThrowExpression;
            }

            return false;
        }

        private bool HasInstanceMembers(INamedTypeSymbol type)
        {
            // Looking for instancce members but excluding constructors.
            return type.GetMembers()
                .Where(m => !m.IsStatic)
                .Any(m => m is IFieldSymbol || m is IPropertySymbol);
        }

        private bool IsInstanceMember(INamedTypeSymbol methodContainingType, ISymbol symbol, Compilation compilation)
        {
            if (symbol.IsStatic)
            {
                return false;
            }

            if (symbol is IMethodSymbol ms && ms.Parameters.Length == 1 && (ms.Name == "Equals" || ms.Name == "CompareTo") && symbol.ContainingType?.Equals(methodContainingType) == true)
            {
                // Special case for Equals(object) => this is MyT && Equals((MyT)other).
                return true;
            }

            if (!(symbol is IPropertySymbol || symbol is IFieldSymbol || symbol is IEventSymbol || symbol is ILocalSymbol ||
                (symbol is IParameterSymbol ps && ps.IsThis)))
            {
                // If symbol is not one of these, it is definitely not an instance member reference
                return false;
            }

            // Still need to check that the member belongs to this type.
            return symbol.ContainingType?.Equals(methodContainingType) == true;
        }
    }
}
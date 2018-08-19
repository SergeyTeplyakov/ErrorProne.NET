using System;
using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.CoreAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class SuspiciousEqualsMethodAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.SuspiciousEqualsMethodImplementation;

        private static readonly string RhsTitle = "Equals method does not use rhs-parameter.";
        private static readonly string RhsMessageFormat = "Equals method does not use parameter {0}.";

        private static readonly string Title = "Equals method does not use any instance members.";
        private static readonly string Description = "Suspicious Equals method implementation that does not uses any instance members.";
        private const string Category = "Correctness";

        // Using warning for visibility purposes
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public SuspiciousEqualsMethodAnalyzer() 
            : base(InstanceMembersAreNotUsedRule, RightHandSideIsNotUsedRule)
        {
        }

        /// <nodoc />
        public static readonly DiagnosticDescriptor InstanceMembersAreNotUsedRule =
            new DiagnosticDescriptor(DiagnosticId, Title, Title, Category, Severity, isEnabledByDefault: true, description: Description);
        
        /// <nodoc />
        public static readonly DiagnosticDescriptor RightHandSideIsNotUsedRule =
            new DiagnosticDescriptor(DiagnosticId, RhsTitle, RhsMessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var method = (IMethodSymbol)context.SemanticModel.GetDeclaredSymbol(context.Node);

            if (method != null)
            {
                // Method overrides System.Equals
                if (method.IsOverride && 
                    method.OverriddenMethod.Name == "Equals" && 
                    (method.OverriddenMethod.ContainingType.IsSystemObject(context.Compilation) ||
                    method.OverriddenMethod.ContainingType.IsSystemValueType(context.Compilation)))
                {
                    var methodSyntax = (MethodDeclarationSyntax)context.Node;
                    if (OnlyThrow(methodSyntax))
                    {
                        // It is ok, if Equals method just throws.
                        return;
                    }

                    var symbols = SymbolExtensions.GetAllUsedSymbols(context.Compilation, methodSyntax.Body).ToList();

                    if (HasInstanceMembers(method.ContainingType) && 
                        symbols.Count(s => IsInstanceMember(method.ContainingType, s)) == 0)
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
                                UnnecessaryWithoutSuggestionDescriptor,
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

        private bool IsInstanceMember(INamedTypeSymbol methodContainingType, ISymbol symbol)
        {
            if (symbol.IsStatic || (!(symbol is IPropertySymbol || symbol is IFieldSymbol || symbol is IEventSymbol)))
            {
                return false;
            }

            return symbol.ContainingType.Equals(methodContainingType);
        }
    }
}
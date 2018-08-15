using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Structs
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EqualsMethodAnalyzer : DiagnosticAnalyzer
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.NoInstanceMembersTouchedByEqualsMethod;

        private static readonly string Title = "Equals method does not touch any instance members.";
        private static readonly string MessageFormat = "Equals method does not touch any instance members";
        private static readonly string Description = "Suspicious Equals method implementation that does not uses instance members.";
        private const string Category = "Correctness";

        // Using warning for visibility purposes
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        public static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
        }

        private void FooBar()
        {
            System.Console.WriteLine("hello");
        }

        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var method = (IMethodSymbol)context.SemanticModel.GetDeclaredSymbol(context.Node);

            if (method != null)
            {
                // Method overrides System.Equals
                if (method.IsOverride && 
                    method.OverriddenMethod.Name == "Equals" && 
                    method.OverriddenMethod.ContainingType.IsSystemObject(context.Compilation))
                {
                    var methodSyntax = (MethodDeclarationSyntax)context.Node;
                    var symbols = SymbolExtensions.GetAllUsedSymbols(context.Compilation, methodSyntax.Body).ToList();

                    if (HasInstanceMembers(method.ContainingType) && 
                        symbols.Count(s => IsInstanceMember(method.ContainingType, s)) == 0)
                    {
                        var diagnostic = Diagnostic.Create(
                            Rule,
                            method.Locations[0]);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private bool HasInstanceMembers(INamedTypeSymbol type)
        {
            throw new NotImplementedException();
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
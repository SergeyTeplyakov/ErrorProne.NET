using System;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Structs
{
    /// <summary>
    /// An analyzer that warns when non-ref-readonly struct is passed using 'in'-modifier.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NonReadOnlyStructPassedAsInParameterAnalyzer : DiagnosticAnalyzer
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.NonReadOnlyStructPassedAsInParameterDiagnosticId;

        private static readonly string Title = "Non-readonly struct used as in-parameter";
        private static readonly string MessageFormat = "Non-readonly struct '{0}' used as in-parameter '{1}'";
        private static readonly string Description = "Non-readonly structs can caused severe performance issues when used as in-parameters";
        private const string Category = "Performance";
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        public static readonly DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);
        
        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            // The following call registers only analysis for top level methods.
            // Need a special logic to cover local functions.
            context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);

            context.RegisterSyntaxNodeAction(AnalyzeLocal, SyntaxKind.LocalFunctionStatement);
        }

        private void AnalyzeMethod(SymbolAnalysisContext context)
        {
            var method = (IMethodSymbol)context.Symbol;
            AnalyzeMethodSymbol(method, d => context.ReportDiagnostic(d));
        }

        private void AnalyzeLocal(SyntaxNodeAnalysisContext context)
        {
            if (context.SemanticModel.GetDeclaredSymbol(context.Node) is IMethodSymbol symbol)
            {
                AnalyzeMethodSymbol(symbol, d => context.ReportDiagnostic(d));
            }
        }

        private static void AnalyzeMethodSymbol(IMethodSymbol method, Action<Diagnostic> reporter)
        {
            if (method.IsOverride || method.IsInterfaceImplementation())
            {
                // This is an override, nothing we can do here
                return;
            }

            foreach (var p in method.Parameters)
            {
                if (p.RefKind == RefKind.In && p.Type.IsValueType && p.Type.UnfriendlyToReadOnlyRefs())
                {
                    // If the method is declared in the struct itself, then we should not warn
                    // because this method can access private state.
                    if (p.ContainingType.Equals(p.Type) && p.Type.HasInstanceFields())
                    {
                        continue;
                    }

                    // Can't just use p.Location, because it will capture just a span for parameter name.
                    var span = p.DeclaringSyntaxReferences[0].GetSyntax().FullSpan;
                    var location = Location.Create(p.DeclaringSyntaxReferences[0].SyntaxTree, span);

                    var diagnostic = Diagnostic.Create(Rule, location, p.Type.Name, p.Name);

                    reporter(diagnostic);
                }
            }
        }
    }
}
using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.StructAnalyzers
{
    /// <summary>
    /// An analyzer that warns when a non-ref-readonly struct is returned by readonly reference.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NonReadOnlyStructReturnedByReadOnlyRefAnalyzer : DiagnosticAnalyzer
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.NonReadOnlyStructReturnedByReadOnlyRefDiagnosticId;

        private static readonly string Title = "A non-readonly struct returned by readonly reference";
        private static readonly string MessageFormat = "A non-readonly struct '{0}' returned by readonly reference";
        private static readonly string Description = "A non-readonly structs can caused severe performance issues when captured in 'ref readonly' variable";
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
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
            context.RegisterSyntaxNodeAction(AnalyzeLocal, SyntaxKind.LocalFunctionStatement);
        }

        private void AnalyzeLocal(SyntaxNodeAnalysisContext context)
        {
            if (context.SemanticModel.GetDeclaredSymbol(context.Node) is IMethodSymbol symbol)
            {
                AnalyzeMethodSymbol(symbol, d => context.ReportDiagnostic(d));
            }
        }

        private void AnalyzeMethod(SymbolAnalysisContext context)
        {
            AnalyzeMethodSymbol((IMethodSymbol)context.Symbol, d => context.ReportDiagnostic(d));
        }

        private static void AnalyzeMethodSymbol(IMethodSymbol method, Action<Diagnostic> diagnosticsReporter)
        {
            if (!method.ReturnsVoid
                && method.ReturnsByRefReadonly
                && method.ReturnType.IsValueType &&
                method.ReturnType.UnfriendlyToReadOnlyRefs())
            {
                var syntaxNode = method.DeclaringSyntaxReferences[0].GetSyntax();
                var syntaxTree = method.DeclaringSyntaxReferences[0].SyntaxTree;

                Location? location = null;

                switch (syntaxNode)
                {
                    case MethodDeclarationSyntax m:
                        location = Location.Create(syntaxTree, m.ReturnType.FullSpan);
                        break;
                    case ArrowExpressionClauseSyntax a:
                        if (a.Parent is PropertyDeclarationSyntax pd)
                        {
                            location = Location.Create(syntaxTree, pd.Type.FullSpan);
                        }

                        break;
                    case LocalFunctionStatementSyntax l:
                        location = Location.Create(syntaxTree, l.ReturnType.Span);
                        break;
                }

                var diagnostic = Diagnostic.Create(Rule, location, method.ReturnType.Name);
                diagnosticsReporter(diagnostic);
            }
        }
    }
}
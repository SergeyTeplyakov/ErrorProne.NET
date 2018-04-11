using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Structs
{
    /// <summary>
    /// An analyzer that warns when a non-ref-readonly struct is returned by readonly reference.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NonReadOnlyStructReturnedByReadOnlyRefAnalyzer : DiagnosticAnalyzer
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.NonReadOnlyStructReturnedByReadOnlyRefDiagnosticId;

        private static readonly string Title = "Non-readonly struct returned by readonly reference";
        private static readonly string MessageFormat = "Non-readonly struct '{0}' returned by readonly reference";
        private static readonly string Description = "Non-readonly structs can caused severe performance issues when captured in ref readonly variable";
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
            context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
        }

        private void AnalyzeMethod(SymbolAnalysisContext context)
        {
            var method = (IMethodSymbol)context.Symbol;
            if (!method.ReturnsVoid 
                && method.ReturnsByRefReadonly 
                && method.ReturnType.IsValueType &&
                method.ReturnType.UnfriendlyToReadOnlyRefs())
            {
                var syntaxNode = method.DeclaringSyntaxReferences[0].GetSyntax();
                var syntaxTree = method.DeclaringSyntaxReferences[0].SyntaxTree;

                Location location = null;
                
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
                }
                //method.MethodKind == MethodKind.PropertyGet
                var diagnostic = Diagnostic.Create(Rule, location, method.ReturnType.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
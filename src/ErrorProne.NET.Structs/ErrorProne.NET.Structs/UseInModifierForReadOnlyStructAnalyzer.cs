using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Structs
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UseInModifierForReadOnlyStructAnalyzer : DiagnosticAnalyzer
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.UseInModifierForReadOnlyStructDiagnosticId;

        private static readonly string Title = "Use in-modifier for a readonly struct";
        private static readonly string MessageFormat = "Use in-modifier for passing readonly struct '{0}'";
        private static readonly string Description = "Readonly structs have better performance when passed readonly reference";
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
            foreach (var p in method.Parameters)
            {
                if (p.RefKind == RefKind.None && p.Type.IsReadOnlyStruct())
                {
                    // Can't just use p.Location, because it will capture just a span for parameter name.
                    var span = p.DeclaringSyntaxReferences[0].GetSyntax().FullSpan;
                    var location = Location.Create(p.DeclaringSyntaxReferences[0].SyntaxTree, span);

                    var diagnostic = Diagnostic.Create(Rule, location, p.Type.Name, p.Name);

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
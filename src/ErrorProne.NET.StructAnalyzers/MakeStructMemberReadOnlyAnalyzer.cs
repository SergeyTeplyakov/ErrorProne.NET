using System.Collections.Immutable;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.StructAnalyzers
{
    /// <summary>
    /// Emits a diagnostic if a struct member can be readonly.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MakeStructMemberReadOnlyAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = DiagnosticIds.MakeStructMemberReadOnly;

        private const string Title = "A struct member can be made readonly";
        private const string MessageFormat = "A {0} can be made readonly";
        private const string Description = "Readonly struct members are more efficient in readonly context by avoiding hidden copies.";
        private const string Category = "Performance";
        
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterSyntaxNodeAction(AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var method = (MethodDeclarationSyntax)context.Node;
            if (ReadOnlyAnalyzer.MethodCanBeReadOnly(method, context.SemanticModel))
            {
                var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method);
                if (methodSymbol != null && ReadOnlyAnalyzer.StructCanBeReadOnly(methodSymbol.ContainingType, context.SemanticModel))
                {
                    // Do not emit the diagnostic if the entire struct can be made readonly.
                    return;
                }

                var memberName = $"method '{method.Identifier.Text}'";
                context.ReportDiagnostic(Diagnostic.Create(Rule, method.Identifier.GetLocation(), memberName));
            }
        }

        private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
        {
            var property = (PropertyDeclarationSyntax)context.Node;

            if (!property.IsGetOnlyAutoProperty() // Excluding int X {get;} because it can be made readonly, but it is already readonly.
                &&ReadOnlyAnalyzer.PropertyCanBeReadOnly(property, context.SemanticModel))
            {
                var propertySymbol = context.SemanticModel.GetDeclaredSymbol(property);

                if (propertySymbol != null && ReadOnlyAnalyzer.StructCanBeReadOnly(propertySymbol.ContainingType, context.SemanticModel))
                {
                    // Do not emit the diagnostic if the entire struct can be made readonly.
                    return;
                }

                var memberName = $"property '{property.Identifier.Text}'";
                context.ReportDiagnostic(Diagnostic.Create(Rule, property.Identifier.GetLocation(), memberName));
            }
        }
    }
}
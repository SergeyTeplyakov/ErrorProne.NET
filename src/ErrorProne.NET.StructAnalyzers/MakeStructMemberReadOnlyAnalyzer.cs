using System;
using System.Collections.Immutable;
using System.Diagnostics;
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

            context.RegisterSyntaxNodeAction(c => DoAnalyze(() => AnalyzePropertyDeclaration(c)), SyntaxKind.PropertyDeclaration);
            context.RegisterSyntaxNodeAction(c => DoAnalyze(() => AnalyzeIndexerDeclaration(c)), SyntaxKind.IndexerDeclaration);
            context.RegisterSyntaxNodeAction(c => DoAnalyze(() => AnalyzeMethodDeclaration(c)), SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var method = (MethodDeclarationSyntax)context.Node;
            if (ReadOnlyAnalyzer.MethodCanBeReadOnly(method, context.SemanticModel))
            {
                var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method);
                if (methodSymbol != null &&
                    ReadOnlyAnalyzer.StructCanBeReadOnly(methodSymbol.ContainingType, context.SemanticModel))
                {
                    // Do not emit the diagnostic if the entire struct can be made readonly.
                    return;
                }

                var memberName = $"method '{method.Identifier.Text}'";
                context.ReportDiagnostic(Diagnostic.Create(Rule, method.Identifier.GetLocation(), memberName));
            }
        }

        private static void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
        {
            var property = (PropertyDeclarationSyntax)context.Node;
            var propertySymbol = context.SemanticModel.GetDeclaredSymbol(property);

            // Excluding int X {get;} because it can be made readonly, but it is already readonly.
            if (property.IsGetOnlyAutoProperty() || propertySymbol == null || property.IsGetSetAutoProperty())
            {
                return;
            }

            if (ReadOnlyAnalyzer.PropertyCanBeReadOnly(property, context.SemanticModel))
            {
                if (!ReadOnlyAnalyzer.StructCanBeReadOnly(propertySymbol.ContainingType, context.SemanticModel))
                {
                    var memberName = $"property '{property.Identifier.Text}'";
                    context.ReportDiagnostic(Diagnostic.Create(Rule, property.Identifier.GetLocation(), memberName));
                }
            }
            else if (property.HasGetterAndSetter())
            {
                foreach (var accessor in property.AccessorList!.Accessors)
                {
                    if (ReadOnlyAnalyzer.AccessorCanBeReadOnly(accessor, context.SemanticModel) &&
                        !ReadOnlyAnalyzer.StructCanBeReadOnly(propertySymbol.ContainingType, context.SemanticModel))
                    {
                        var memberName = $"property '{property.Identifier.Text}'";
                        context.ReportDiagnostic(Diagnostic.Create(Rule, accessor.Keyword.GetLocation(), memberName));
                    }
                }
            }
        }
        
        private static void AnalyzeIndexerDeclaration(SyntaxNodeAnalysisContext context)
        {
            var property = (IndexerDeclarationSyntax)context.Node;
            var propertySymbol = context.SemanticModel.GetDeclaredSymbol(property);
            if (propertySymbol == null || property.IsGetSetAutoProperty())
            {
                return;
            }

            if (ReadOnlyAnalyzer.IndexerCanBeReadOnly(property, context.SemanticModel))
            {
                if (!ReadOnlyAnalyzer.StructCanBeReadOnly(propertySymbol.ContainingType, context.SemanticModel))
                {
                    var memberName = "indexer";
                    context.ReportDiagnostic(Diagnostic.Create(Rule, property.ThisKeyword.GetLocation(), memberName));
                }
            }
            else if (property.HasGetterAndSetter() && !property.IsGetSetAutoProperty())
            {
                foreach (var accessor in property.AccessorList!.Accessors)
                {
                    if (ReadOnlyAnalyzer.AccessorCanBeReadOnly(accessor, context.SemanticModel) &&
                        !ReadOnlyAnalyzer.StructCanBeReadOnly(propertySymbol.ContainingType, context.SemanticModel))
                    {
                        var memberName = "indexer";
                        context.ReportDiagnostic(Diagnostic.Create(Rule, accessor.Keyword.GetLocation(), memberName));
                    }
                }
            }
        }

        private static void DoAnalyze(Action action)
        {
            try
            {
                // Avoiding the following exception from the middle of Roslyn infra:
                // 'Unable to cast object of type 'Microsoft.CodeAnalysis.CSharp.Symbols.Metadata.PE.PEAssemblySymbol' to type 'Microsoft.CodeAnalysis.CSharp.Symbols.SourceAssemblySymbol'.'
                action();
            }
            catch (InvalidCastException)
            {
            }
        }
    }
}
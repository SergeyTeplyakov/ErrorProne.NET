using System.Collections.Immutable;
using System.ComponentModel.Design.Serialization;
using System.Globalization;
using ErrorProne.NET.Core;
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
    /// <remarks>
    /// Like the other diagnostics that warn about potential hidden copies, this analyzer warns only for members of large structs to avoid too much noise.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MakeStructMemberReadOnlyAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EPS12";

        private const string Title = "A struct member can be made readonly";
        private const string MessageFormat = "A {0} can be made readonly for struct {1} of size {2}";
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
            
            context.RegisterSyntaxNodeAction(context => AnalyzePropertyDeclaration(context), SyntaxKind.PropertyDeclaration);
            context.RegisterSyntaxNodeAction(context => AnalyzeMethodDeclaration(context), SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var method = (MethodDeclarationSyntax)context.Node;
            if (method.Identifier.Text == "ToDebuggerDisplay")
            {
                // Analyzing ToDebuggerDisplay methods causes InvalidCastException in the middle of Roslyn infrastructure.
                return;
            }

            if (ReadOnlyAnalyzer.MethodCanBeReadOnly(method, context.SemanticModel))
            {
                var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method);
                if (methodSymbol is null)
                {
                    return;
                }
                if (ReadOnlyAnalyzer.StructCanBeReadOnly(methodSymbol.ContainingType, context.SemanticModel))
                {
                    // Do not emit the diagnostic if the entire struct can be made readonly.
                    return;
                }

                var largeStructThreshold = Settings.GetLargeStructThresholdOrDefault(method.TryGetAnalyzerConfigOptions(context.Options));
                ReportAnalyzerForLargeStruct(context, largeStructThreshold, methodSymbol, $"method '{method.Identifier.Text}'", method.Identifier.GetLocation());
            }
        }

        private static void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
        {
            var property = (PropertyDeclarationSyntax)context.Node;

            if (!property.IsGetOnlyAutoProperty() // Excluding int X {get;} because it can be made readonly, but it is already readonly.
                &&ReadOnlyAnalyzer.PropertyCanBeReadOnly(property, context.SemanticModel))
            {
                var propertySymbol = context.SemanticModel.GetDeclaredSymbol(property);
                if (propertySymbol is null)
                {
                    return;
                }

                if (ReadOnlyAnalyzer.StructCanBeReadOnly(propertySymbol.ContainingType, context.SemanticModel))
                {
                    // Do not emit the diagnostic if the entire struct can be made readonly.
                    return;
                }
                
                var largeStructThreshold = Settings.GetLargeStructThresholdOrDefault(property.TryGetAnalyzerConfigOptions(context.Options));
                ReportAnalyzerForLargeStruct(context, largeStructThreshold, propertySymbol, $"property '{property.Identifier.Text}'", property.Identifier.GetLocation());
            }
        }

        private static void ReportAnalyzerForLargeStruct(SyntaxNodeAnalysisContext context, int largeStructThreshold, ISymbol memberSymbol, string memberName, Location memberLocation)
        {
            if (memberSymbol.ContainingType.IsLargeStruct(context.Compilation, largeStructThreshold, out var structSize))
            {
                var typeName = memberSymbol.ContainingType.Name;
                context.ReportDiagnostic(Diagnostic.Create(Rule, memberLocation, memberName, typeName, structSize));
            }
        }
    }
}
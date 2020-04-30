using System;
using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.StructAnalyzers
{
    /// <summary>
    /// An analyzer that warns when a struct with the default implementation of <see cref="object.Equals(object)"/> or <see cref="object.GetHashCode()"/> is used
    /// in another structs <see cref="object.Equals(object)"/> or <see cref="object.GetHashCode"/> methods.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DefaultEqualsOrHashCodeIsUsedInStructAnalyzer : DiagnosticAnalyzer
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.DefaultEqualsOrHashCodeIsUsedInStructDiagnosticId;

        private const string Title = "Default 'ValueType.Equals' or 'HashCode' is used for struct equality";
        private const string MessageFormat = "The default 'ValueType.{0}' is used in {1}.";
        private const string Description = "The default implementation of 'Equals' and 'GetHashCode' for structs is inefficient and could cause severe performance issues.";
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
        }


        private void AnalyzeMethod(SymbolAnalysisContext context)
        {
            if (context.Symbol is IMethodSymbol ms && 
                ms.TryGetDeclarationSyntax() is var syntax &&
                syntax != null &&
                context.TryGetSemanticModel(out var semanticModel))
            {
                TryAnalyzeEqualsOrGetHashCode(ms, syntax, semanticModel, d => context.ReportDiagnostic(d));
            }
        }

        private void TryAnalyzeEqualsOrGetHashCode(IMethodSymbol methodSymbol, MethodDeclarationSyntax syntax,
            SemanticModel semanticModel, Action<Diagnostic> diagnosticReporter)
        {
            if (
                // Overrides Equals or GetHashCode
                (methodSymbol.IsOverride &&
                (methodSymbol.Name == nameof(Equals) || methodSymbol.Name == nameof(GetHashCode))) ||
                // Or implements IEquatable<T>.Equals
                (methodSymbol.IsInterfaceImplementation() && methodSymbol.Name == nameof(Equals))
                )
            {
                // Looking through all the method calls in the current method declaration
                foreach (var node in syntax.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
                {
                    if (node.Expression is MemberAccessExpressionSyntax ms)
                    {
                        // Interested only 'a.b()'
                        var s = semanticModel.GetSymbolInfo(ms.Expression).Symbol;

                        // Looking for a field access with the calls to Equals/GetHashCode
                        // on structs with default Equals/GetHashCode implementations.
                        if (s is IFieldSymbol fs &&
                            fs.Type.IsStruct() &&
                            fs.Type.HasDefaultEqualsOrHashCodeImplementations(out _) &&
                            semanticModel.GetSymbolInfo(ms).Symbol is var reference &&
                            reference is IMethodSymbol referencedMethod &&

                            (referencedMethod.Name == nameof(Equals) || referencedMethod.Name == nameof(GetHashCode)))
                        {
                            string equalsOrHashCodeAsString = referencedMethod.Name;
                            var diagnostic = Diagnostic.Create(Rule, ms.Name.GetLocation(), equalsOrHashCodeAsString, $"{methodSymbol.ContainingType.ToDisplayString()}.{referencedMethod.Name}");
                            diagnosticReporter(diagnostic);
                        }
                    }
                }
            }
        }
    }
}
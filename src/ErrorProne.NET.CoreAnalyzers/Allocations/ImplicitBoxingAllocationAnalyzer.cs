using System;
using System.Collections.Immutable;
using ErrorProne.NET.AsyncAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

#nullable enable

namespace ErrorProne.NET.CoreAnalyzers.Allocations
{
    /// <summary>
    /// Analyzer that warns when the result of a method invocation is ignore (when it potentially, shouldn't).
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed partial class ImplicitBoxingAllocationAnalyzer : DiagnosticAnalyzer
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.ImplicitBoxing;

        private static readonly string Title = "Implicit boxing allocation.";
        private static readonly string Message = "Implicit boxing allocation of type '{0}': {1}.";

        private static readonly string Description = "Implicit boxing allocation can cause performance issues if happened on application hot paths.";
        private const string Category = "Performance";

        // Using warning for visibility purposes
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        public static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, Message, Category, Severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <nodoc />
        public ImplicitBoxingAllocationAnalyzer() 
            //: base(supportFading: false, diagnostics: Rule)
        {
        }

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            RegisterImplicitBoxingOperations(context);

            context.RegisterSyntaxNodeAction(AnalyzeInvocationExpression, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context)
        {
            if (context.ShouldNotDetectAllocationsFor())
            {
                return;
            }

            var invocation = (InvocationExpressionSyntax)context.Node;

            var targetSymbol = context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol;

            // Checking for foo.Bar() patterns that cause boxing allocations.

            if (targetSymbol != null && invocation.Expression is MemberAccessExpressionSyntax ms)
            {
                
                var sourceOperation = context.SemanticModel.GetOperation(ms.Expression);

                if (sourceOperation?.Type != null)
                {
                    if (
                        // Boxing allocation occurs when the CLR calls a method for as struct that is defined in a value type.
                        // For instance, for non-overriden methods like ToString, GetHashCode, Equals
                        // or for calling methods defined in System.Enum type.
                        sourceOperation.Type.IsValueType
                        && targetSymbol.ContainingType?.IsValueType == false

                        // Excluding the case when the method is called on generics.
                        && sourceOperation.Type.Kind != SymbolKind.TypeParameter
                        
                        // Excluding the case when a calling method is an extension method.
                        && !(targetSymbol is IMethodSymbol method && method.IsExtensionMethod)
                        // myStruct.GetType() is causing allocation only with 32-bits legacy jitter with full framework,
                        // and because this is the least used jitter (IMO) we decided exclude this case
                        // and not warn on it.
                        && targetSymbol.Name != nameof(GetType)
                        )
                    {
                        string reason = $"calling an instance method inherited from {targetSymbol.ContainingType.ToDisplayString()}";
                        if (targetSymbol.Name == nameof(Enum.HasFlag))
                        {
                            reason += " (not applicable for Core CLR)";
                        }
                        
                        context.ReportDiagnostic(Diagnostic.Create(Rule, ms.Name.GetLocation(), sourceOperation.Type.Name, reason));
                    }
                }
            }
        }
    }
}
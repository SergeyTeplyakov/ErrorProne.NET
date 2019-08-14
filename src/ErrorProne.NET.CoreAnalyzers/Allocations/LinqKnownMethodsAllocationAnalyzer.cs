using System.Collections.Generic;
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
    public sealed class LinqKnownMethodsAllocationAnalyzer : DiagnosticAnalyzer
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.LinqAllocation;

        private static readonly string Title = "LINQ method with known invocation.";
        private static readonly string Message = "LINQ method {0} is known to cause allocations.";

        private static readonly string Description = "LINQ query is known to cause allocations.";
        private const string Category = "CodeSmell";

        // Using warning for visibility purposes
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        public static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, Message, Category, Severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <nodoc />
        public LinqKnownMethodsAllocationAnalyzer() 
        {
        }

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeInvocationExpression, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context)
        {
            if (NoHiddenAllocationsConfiguration.ShouldNotDetectAllocationsFor(context.Node, context.SemanticModel))
            {
                return;
            }

            var invocation = (InvocationExpressionSyntax)context.Node;

            var targetSymbol = context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol;

            if (targetSymbol?.ContainingType?.ToDisplayString() == "System.Linq.Enumerable" && invocation.Expression is MemberAccessExpressionSyntax ms)
            {
                if (targetSymbol.Name == "Count")
                {
                    var sourceOperation = context.SemanticModel.GetOperation(ms.Expression);

                    if (sourceOperation?.Type != null)
                    {
                        var interfaces = sourceOperation.Type.AllInterfaces;
                        foreach (var i in interfaces)
                        {
                            if (i.ToDisplayString() == "System.Collections.ICollection")
                            {
                                return; // ICollection.Count is optimized and does not cause allocations.
                            }
                        }
                    }
                }

                context.ReportDiagnostic(Diagnostic.Create(Rule, ms.Name.GetLocation(), targetSymbol.ToDisplayString()));
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.ContractsLight;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

#nullable enable

namespace ErrorProne.NET.CoreAnalyzers.Allocations
{
    internal static class ClosureAnalysisExtensions
    {
        /// <summary>
        /// Returns true if the only captured variable is 'this' "pointer".
        /// </summary>
        public static bool CapturesOnlyThis(this DataFlowAnalysis dataflow)
        {
            return dataflow.Captured.Length == 1 && dataflow.Captured[0] is IParameterSymbol ps && ps.IsThis;
        }
    }

    /// <summary>
    /// Analyzer that warns when the result of a method invocation is ignore (when it potentially, shouldn't).
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ClosureAllocationAnalyzer : DiagnosticAnalyzer
    {
        private const string DiagnosticId = DiagnosticIds.ClosureAllocation;

        private static readonly string Title = "Closure allocation.";
        private static readonly string Message = "Closure allocation that captures {0}.";

        private static readonly string Description = "Closure allocation.";
        private const string Category = "Performance";

        // Using warning for visibility purposes
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        private static readonly DiagnosticDescriptor ClosureAllocationRule =
            new DiagnosticDescriptor(DiagnosticId, Title, Message, Category, Severity, isEnabledByDefault: true, description: Description);


        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ClosureAllocationRule);

        /// <nodoc />
        public ClosureAllocationAnalyzer() 
        {
        }

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            context.RegisterOperationAction(AnalyzeAnonymousFunction, OperationKind.AnonymousFunction);
            context.RegisterOperationAction(AnalyzeLocalFunction, OperationKind.LocalFunction);
        }

        private void AnalyzeLocalFunction(OperationAnalysisContext context)
        {
            var operation = context.Operation;
            var dataflow = operation.SemanticModel.AnalyzeDataFlow(operation.Syntax);
            if (dataflow.Captured.Length != 0 && !dataflow.CapturesOnlyThis())
            {
                // The local functions are different from anonymous methods.
                // By default, the generated display "class" is actually a struct.
                // But if the local function is converted to a delegate then the compiler
                // generates a class.

                // (dco.Target as IMemberReferenceOperation).Member.Equals((context.Operation as ILocalFunctionOperation).Symbol)
                var localFunctionSymbol = ((ILocalFunctionOperation) operation).Symbol;
                var localFunctionToDelegateConversions =
                    operation.Parent.Descendants()
                        .Where(o =>
                            o is IDelegateCreationOperation dco && dco.Target is IMemberReferenceOperation mr &&
                            mr.Member.Equals(localFunctionSymbol));
                if (localFunctionToDelegateConversions.Any())
                {
                    string captures = string.Join(", ", dataflow.Captured.Select(c => $"'{c.Name}'"));
                    
                    Contract.Assert(operation.Parent.Syntax is BlockSyntax, "Local function's parent should be a block");
                    var location = ((BlockSyntax)operation.Parent.Syntax).OpenBraceToken.GetLocation();

                    string message =
                        $"{captures} because local function '{localFunctionSymbol.Name}' is converted to a delegate";
                    context.ReportDiagnostic(Diagnostic.Create(ClosureAllocationRule, location, message));
                }
            }
        }

        private void AnalyzeAnonymousFunction(OperationAnalysisContext context)
        {
            var operation = context.Operation;

            var dataflow = operation.SemanticModel.AnalyzeDataFlow(operation.Syntax);
            if (dataflow.Captured.Length != 0)
            {
                if (dataflow.CapturesOnlyThis())
                {
                    // If the only captured variable is 'this' pointer, then
                    // we should not be emitting any diagnostics.
                    // Consider the following case:
                    // private static int s = 42;
                    // private int i = 42;
                    // public void M(int y)
                    // {
                    //    // Delegate allocation, but no closure allocations.
                    //    System.Func<int> f = () => s + i;
                    //    f();
                    // }
                    // In this case, an anonymous method is generated directly in the enclosing type
                    // and not in the generate "closure" type.
                    // So in this case, the delegate is allocated, but there is no closure allocations.
                    return;
                }

                // Current anonymous method captures external context
                // Group captures per scope, because the compiler creates new display class instance
                // per scope.
                Dictionary<SyntaxNode?, List<ISymbol>> closuresPerScope =
                    dataflow.Captured
                        .Select(c => (scope: c.GetScopeForDisplayClass(), capture: c))
                        .Where(tpl => tpl.scope != null)
                        // Grouping by the first element to create scope to symbols map.
                        .GroupToDictionary();

                foreach (var kvp in closuresPerScope)
                {
                    var locationSyntax = kvp.Key;
                    Location location = locationSyntax switch
                    {
                        BlockSyntax block => block.OpenBraceToken.GetLocation(),
                        ArrowExpressionClauseSyntax arrow => arrow.ArrowToken.GetLocation(),
                        ForEachStatementSyntax forEach => forEach.Statement is BlockSyntax block ? block.OpenBraceToken.GetLocation() : forEach.ForEachKeyword.GetLocation(),
                        _ => locationSyntax!.GetLocation(),
                    };

                    string captures = string.Join(", ", kvp.Value.Select(c => $"'{c.Name}'"));

                    context.ReportDiagnostic(Diagnostic.Create(ClosureAllocationRule, location, captures));
                }
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.ContractsLight;
using System.Linq;
using ErrorProne.NET.AsyncAnalyzers;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

#nullable enable

namespace ErrorProne.NET.CoreAnalyzers.Allocations
{
    /// <summary>
    /// Analyzer that warns when the result of a method invocation is ignore (when it potentially, shouldn't).
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DelegateAllocationAnalyzer : DiagnosticAnalyzer
    {
        private const string Category = "Performance";
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        private const string DiagnosticId = DiagnosticIds.DelegateAllocation;

        private static readonly string Title = "Delegate allocation.";
        private static readonly string Message = "Delegate allocation caused by {0}.";

        private static readonly string Description = "Delegate allocation.";

        private static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, Message, Category, Severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <nodoc />
        public DelegateAllocationAnalyzer() 
        {
        }

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.RegisterOperationAction(DelegateCreation, OperationKind.DelegateCreation);
            context.RegisterOperationAction(AnalyzeAnonymousFunction, OperationKind.AnonymousFunction);
        }

        private void DelegateCreation(OperationAnalysisContext context)
        {
            if (NoHiddenAllocationsConfiguration.ShouldNotDetectAllocationsFor(context.Operation))
            {
                return;
            }

            // This method is called for anonymous methods as well,
            // but this method only handles method group conversion
            // and the next method deals with delegate allocations.
            // It is easier to do the rest there because we don't want
            // to emit warnings for non-capturing anonymous methods.
            if (context.Operation is IDelegateCreationOperation delegateCreation && delegateCreation.Target.Kind == OperationKind.MethodReference)
            {
                // This is a method group conversion.
                context.ReportDiagnostic(Diagnostic.Create(Rule, context.Operation.Syntax.GetLocation(), "method group conversion"));
            }
        }

        private void AnalyzeAnonymousFunction(OperationAnalysisContext context)
        {
            // TODO: revisit this implementation! Maybe this can be done in DelegateCreation.
            var operation = context.Operation;

            if (NoHiddenAllocationsConfiguration.ShouldNotDetectAllocationsFor(operation))
            {
                return;
            }

            var dataflow = operation.SemanticModel.AnalyzeDataFlow(operation.Syntax);
            if (dataflow.Captured.Length != 0)
            {
                string? explanation = null;
                if (dataflow.Captured.Length == 1 && dataflow.Captured[0] is IParameterSymbol ps && ps.IsThis)
                {
                    // The only captured variable is 'this'.
                    // In this case the delegate is created (not cached) but a closure is not created/generated.
                    explanation = "capturing of 'this' reference (no display class is created)";
                }
                else
                {
                    var symbols = string.Join(", ",
                        dataflow.Captured.Where(c => !(c is IParameterSymbol ps && ps.IsThis))
                            .Select(s => $"'{s.Name}'"));

                    explanation = $"capturing of {symbols} (display class is created)";
                }

                var syntax = context.Operation.Syntax;
                if (syntax != null)
                {
                    Location location = syntax switch
                    {
                        LambdaExpressionSyntax ls => ls.ArrowToken.GetLocation(),
                        AnonymousMethodExpressionSyntax am => am.DelegateKeyword.GetLocation(),
                        _ => syntax.GetLocation(),
                    };

                    Contract.Assert(explanation != null);
                    context.ReportDiagnostic(Diagnostic.Create(Rule, location, explanation));
                }
            }
            //    // Current anonymous method captures external context
            //    // Group captures per scope, because the compiler creates new display class instance
            //    // per scope.

            //    Dictionary<SyntaxNode?, List<ISymbol>> closuresPerScope =
            //        dataflow.Captured
            //            .Select(c => (scope: c.GetScopeForDisplayClass(), capture: c))
            //            .Where(tpl => tpl.scope != null)
            //            // Grouping by the first element to create scope to symbols map.
            //            .GroupToDictionary();

            //    foreach (var kvp in closuresPerScope)
            //    {
            //        var locationSyntax = kvp.Key;
            //        Location location = locationSyntax switch
            //        {
            //            BlockSyntax block => block.OpenBraceToken.GetLocation(),
            //            ArrowExpressionClauseSyntax arrow => arrow.ArrowToken.GetLocation(),
            //            ForEachStatementSyntax forEach => forEach.Statement is BlockSyntax block ? block.OpenBraceToken.GetLocation() : forEach.ForEachKeyword.GetLocation(),
            //            _ => locationSyntax!.GetLocation(),
            //        };

            //        string captures = string.Join(", ", kvp.Value.Select(c => $"'{c.Name}'"));

            //        context.ReportDiagnostic(Diagnostic.Create(ClosureAllocationRule, location, captures));
            //    }
            //}
        }
    }
}
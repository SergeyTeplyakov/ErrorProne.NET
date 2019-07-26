using System;
using System.Collections.Immutable;
using System.Reflection;
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
    public sealed class ImplicitCastBoxingAllocationAnalyzer : DiagnosticAnalyzer
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.ExplicitCastBoxing;

        private static readonly string Title = "Explicit cast boxing allocation.";
        private static readonly string Message = "Boxing allocation of type '{0}' because of implicit cast to type '{1}'.";

        private static readonly string Description = "Boxing is happening because of an implicit cast from value type to non value type.";
        private const string Category = "CodeSmell";

        // Using warning for visibility purposes
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        public static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, Message, Category, Severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <nodoc />
        public ImplicitCastBoxingAllocationAnalyzer() 
            //: base(supportFading: false, diagnostics: Rule)
        {
        }

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            context.RegisterOperationAction(AnalyzeOperation, OperationKind.Conversion);
            context.RegisterOperationAction(AnalyzeInterpolation, OperationKind.Interpolation);
            context.RegisterSyntaxNodeAction(AnalyzeForEachLoop, SyntaxKind.ForEachStatement);
            context.RegisterOperationAction(AnalyzeMethodReference, OperationKind.MethodReference);
        }

        private void AnalyzeMethodReference(OperationAnalysisContext context)
        {
            if (NoHiddenAllocationsConfiguration.TryGetConfiguration(context.Operation) != NoHiddenAllocationsLevel.Default)
            {
                return;
            }

            var methodReference = (IMethodReferenceOperation)context.Operation;

            if (methodReference.Instance?.Type?.IsValueType == true && !methodReference.Member.IsStatic)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, methodReference.Instance.Syntax.GetLocation(), methodReference.Instance.Type.ToDisplayString(), "object"));
            }
        }

        private void AnalyzeOperation(OperationAnalysisContext context)
        {
            if (NoHiddenAllocationsConfiguration.TryGetConfiguration(context.Operation) != NoHiddenAllocationsLevel.Default)
            {
                return;
            }

            var conversion = (IConversionOperation)context.Operation;

            var targetType = conversion.Type;
            var operandType = conversion.Operand.Type;

            if (conversion.IsImplicit && !conversion.Conversion.IsUserDefined && operandType?.IsValueType == true && targetType?.IsReferenceType == true)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, conversion.Operand.Syntax.GetLocation(), operandType.ToDisplayString(), targetType.ToDisplayString()));
            }
            else if (conversion.IsImplicit && operandType?.IsTupleType == true && targetType?.IsTupleType == true)
            {
                var operandTypes = operandType.GetTupleTypes();
                var targetTypes = targetType.GetTupleTypes();

                for (var i = 0; i < operandTypes.Length; i++)
                {
                    if (operandTypes[i].IsValueType && targetTypes[i].IsReferenceType)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Rule, conversion.Operand.Syntax.GetLocation(), operandType.ToDisplayString(), targetType.ToDisplayString()));
                        break;
                    }
                }
            }
        }

        private void AnalyzeForEachLoop(SyntaxNodeAnalysisContext context)
        {
            if (NoHiddenAllocationsConfiguration.TryGetConfiguration(context.Node, context.SemanticModel) != NoHiddenAllocationsLevel.Default)
            {
                return;
            }

            var foreachLoop = (IForEachLoopOperation)context.SemanticModel.GetOperation(context.Node);

            ITypeSymbol elementType = foreachLoop.GetElementType();
            if (elementType?.IsValueType == true && foreachLoop.GetConversionInfo()?.IsBoxing == true)
            {
                var targetTypeName = "Unknown";
                if (foreachLoop.LoopControlVariable is IVariableDeclaratorOperation op && op.Symbol.Type != null)
                {
                    targetTypeName = op.Symbol.Type.ToDisplayString();
                }

                context.ReportDiagnostic(Diagnostic.Create(Rule, foreachLoop.Collection.Syntax.GetLocation(), elementType.ToDisplayString(), targetTypeName));
            }
        }

        private void AnalyzeInterpolation(OperationAnalysisContext context)
        {
            if (NoHiddenAllocationsConfiguration.TryGetConfiguration(context.Operation) != NoHiddenAllocationsLevel.Default)
            {
                return;
            }
            // Covering cases when string interpolation is causing boxing, like $"{42}";

            if (context.Operation is IInterpolationOperation interpolationOperation && interpolationOperation.Expression.Type?.IsValueType == true)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, interpolationOperation.Expression.Syntax.GetLocation(), interpolationOperation.Expression.Type.ToDisplayString(), "object"));
            }
        }
    }
}
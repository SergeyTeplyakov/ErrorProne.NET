using ErrorProne.NET.AsyncAnalyzers;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

#nullable enable

namespace ErrorProne.NET.CoreAnalyzers.Allocations
{
    partial class ImplicitBoxingAllocationAnalyzer
    {
        private void RegisterImplicitBoxingOperations(AnalysisContext context)
        {
            context.RegisterOperationAction(AnalyzeOperation, OperationKind.Conversion);
            context.RegisterOperationAction(AnalyzeInterpolation, OperationKind.Interpolation);
            context.RegisterSyntaxNodeAction(AnalyzeForEachLoop, SyntaxKind.ForEachStatement);
            context.RegisterOperationAction(AnalyzeMethodReference, OperationKind.MethodReference);
        }

        private void AnalyzeMethodReference(OperationAnalysisContext context)
        {
            if (NoHiddenAllocationsConfiguration.ShouldNotDetectAllocationsFor(context.Operation))
            {
                return;
            }

            var methodReference = (IMethodReferenceOperation)context.Operation;

            // This is a method group conversion from a struct's instance method.
            if (methodReference.Instance?.Type?.IsValueType == true && !methodReference.Member.IsStatic)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, methodReference.Instance.Syntax.GetLocation(), methodReference.Instance.Type.ToDisplayString(), "object"));
            }
        }

        private void AnalyzeOperation(OperationAnalysisContext context)
        {
            if (NoHiddenAllocationsConfiguration.ShouldNotDetectAllocationsFor(context.Operation))
            {
                return;
            }

            var conversion = (IConversionOperation)context.Operation;

            var targetType = conversion.Type;
            var operandType = conversion.Operand.Type;

            if (conversion.IsImplicit &&
                operandType?.IsValueType == true && 
                targetType?.IsReferenceType == true
                // User-defined conversions are fine: if they cause allocations then the conversion itself should be marked with NoHiddenAllocations.    
                && !conversion.Conversion.IsUserDefined)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, conversion.Operand.Syntax.GetLocation(), operandType.ToDisplayString(), targetType.ToDisplayString()));
            }

            if (conversion.IsImplicit && operandType?.IsTupleType == true && targetType?.IsTupleType == true)
            {
                var operandTypes = operandType.GetTupleTypes();
                var targetTypes = targetType.GetTupleTypes();

                for (var i = 0; i < operandTypes.Length; i++)
                {
                    if (operandTypes[i].IsValueType && targetTypes[i].IsReferenceType)
                    {
                        // This is the following case:
                        // (object, object) foo() => (1, 2);
                        context.ReportDiagnostic(Diagnostic.Create(Rule, conversion.Operand.Syntax.GetLocation(), operandType.ToDisplayString(), targetType.ToDisplayString()));
                        break;
                    }
                }
            }
        }

        private void AnalyzeForEachLoop(SyntaxNodeAnalysisContext context)
        {
            if (NoHiddenAllocationsConfiguration.ShouldNotDetectAllocationsFor(context.Node, context.SemanticModel))
            {
                return;
            }

            // foreach loop can cause boxing allocation in the following case:
            // foreach(object o in Enumerable.Range(1, 10))
            // In this case, all the elements are boxed to a target type.

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
            if (NoHiddenAllocationsConfiguration.ShouldNotDetectAllocationsFor(context.Operation))
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
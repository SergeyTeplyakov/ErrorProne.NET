using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis.Operations;
using ErrorProne.NET.CoreAnalyzers;

namespace ErrorProne.NET.AsyncAnalyzers
{
    /// <summary>
    /// Warns when a method that returns a Task-like type (Task, Task&lt;T&gt;, ValueTask, etc.) returns null.
    /// The analyzer is useful only when non-nullable reference types are not enabled!
    /// If they are enabled, the compiler will already warn about returning null for a non-nullable return type.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DoNotReturnNullForTaskLikeAnalyzer : DiagnosticAnalyzerBase
    {
        private static readonly DiagnosticDescriptor Rule = DiagnosticDescriptors.EPC31;

        /// <nodoc />
        public DoNotReturnNullForTaskLikeAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            
            context.RegisterOperationAction(AnalyzeReturnOperation, OperationKind.Return);
        }

        private void AnalyzeReturnOperation(OperationAnalysisContext context)
        {
            var returnOperation = (IReturnOperation)context.Operation;
            
            // In case of 'return;'
            if (returnOperation.ReturnedValue == null)
            {
                return;
            }

            //var semanticModel = context.Compilation.GetSemanticModel(returnOperation.Syntax.SyntaxTree, false);
            
            

            // We don't need to analyze the method (which might be expensive), but we can just check the type of the return value.
            // And do nothing if it's not a Task-like type.
            var returnType = returnOperation.ReturnedValue?.Type;
            if (returnType == null || returnType.NullableAnnotation == NullableAnnotation.Annotated)
            {
                // Not running this analysis when nullable types are on.
                // In this case, the compiler will emit a warning.
                return;
            }

            if (returnType.IsTaskLike(context.Compilation) != true)
            {
                return;
            }
            
            var returnedValueOperation = returnOperation.ReturnedValue;
            IMethodSymbol? methodSymbol = findParentLocalOrLambdaSymbol(returnedValueOperation) ?? context.ContainingSymbol as IMethodSymbol;
            if (methodSymbol is not null && !methodSymbol.IsAsync)
            {
                if (isReturningNull(returnedValueOperation, context.Compilation) && methodSymbol.ReturnType.IsTaskLike(context.Compilation))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, returnOperation.Syntax.GetLocation(), methodSymbol.Name));
                }
            }

            static bool isReturningNull(IOperation? operation, Compilation compilation)
            {
                if (operation == null)
                {
                    return false;
                }

                if (operation is IReturnOperation returnedValueOperation)
                {
                    // Case A: Check if the returned value is a constant null.
                    if (returnedValueOperation.ConstantValue.HasValue && returnedValueOperation.ConstantValue.Value == null)
                    {
                        return true;
                    }

                    
                }

                // Case B: Check for the 'default' keyword used with a reference type.
                // This handles 'return default;' where the method's return type is a class, interface, delegate, or array.
                if (operation is IDefaultValueOperation &&
                    operation.Type?.IsReferenceType == true)
                {
                    return true;
                }

                if (operation.ConstantValue.HasValue && operation.ConstantValue.Value is null)
                {
                    return true;
                }

                if (operation is IConversionOperation conversion)
                {
                    if (conversion.Type.IsTaskLike(compilation))
                    {
                        return false;
                    }

                    return isReturningNull(conversion.Operand, compilation);
                }

                if (operation is IConditionalAccessOperation conditionalAccess)
                {
                    return isReturningNull(conditionalAccess.Operation, compilation) || isReturningNull(conditionalAccess.WhenNotNull, compilation);
                }
                else if (operation is IConditionalOperation conditional)
                {
                    return isReturningNull(conditional.WhenTrue, compilation) || isReturningNull(conditional.WhenFalse, compilation);
                }
                else if (operation is ISwitchExpressionOperation switchExpression)
                {
                    foreach (var arm in switchExpression.Arms)
                    {
                        if (isReturningNull(arm.Value, compilation))
                        {
                            return true;
                        }
                    }
                }

                return false;

            }

            static IMethodSymbol? findParentLocalOrLambdaSymbol(IOperation? operation)
            {
                foreach (var parent in operation.EnumerateParentOperations())
                {
                    if (parent is ILocalFunctionOperation lf)
                    {
                        return lf.Symbol;
                    }

                    if (parent is IAnonymousFunctionOperation f)
                    {
                        return f.Symbol;
                    }
                }

                return null;
            }
        }
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// Base class for analyzers that check if method results should be observed but are not.
    /// </summary>
    public abstract class UnobservedResultAnalyzerBase : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        protected UnobservedResultAnalyzerBase(DiagnosticDescriptor descriptor)
            : base(descriptor)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterOperationAction(AnalyzeExpressionStatement, OperationKind.ExpressionStatement);
        }

        private void AnalyzeExpressionStatement(OperationAnalysisContext context)
        {
            var expressionStatement = (IExpressionStatementOperation)context.Operation;
            
            // Check if the expression statement contains an invocation that's not being used
            if (expressionStatement.Operation is IInvocationOperation invocation)
            {
                AnalyzeInvocation(invocation, context);
            }
            // Also check for await expressions that wrap invocations
            else if (expressionStatement.Operation is IAwaitOperation awaitOperation)
            {
                AnalyzeAwaitOperation(awaitOperation, context);
            }
        }

        private void AnalyzeInvocation(IInvocationOperation invocation, OperationAnalysisContext context)
        {
            if (!ShouldAnalyzeMethod(invocation.TargetMethod, context.Compilation))
            {
                return;
            }

            if (ResultObservedByExtensionMethod(invocation, context.Compilation))
            {
                // Result is observed!
                return;
            }

            if (IsException(invocation.Type, context.Compilation) &&
                invocation.TargetMethod.ConstructedFrom.ReturnType is ITypeParameterSymbol)
            {
                // The inferred type is System.Exception (or one of it's derived types),
                // but the operation that is called is generic (i.e. exception type was inferred).
                // It means that there is nothing special about the return type and it can be ignored.
                return;
            }

            var diagnostic = CreateDiagnostic(invocation);
            context.ReportDiagnostic(diagnostic);
        }

        private void AnalyzeAwaitOperation(IAwaitOperation awaitOperation, OperationAnalysisContext context)
        {
            if (awaitOperation.Type == null)
            {
                return;
            }

            // For await expressions, we need to check if the awaited result should be observed
            IMethodSymbol? method = null;
            if (awaitOperation.Operation is IInvocationOperation invocation)
            {
                method = invocation.TargetMethod;
            }

            if (!ShouldAnalyzeAwaitedResult(awaitOperation.Type, method, context.Compilation))
            {
                return;
            }

            if (awaitOperation.Operation is IInvocationOperation invocationOp &&
                ResultObservedByExtensionMethod(invocationOp, context.Compilation))
            {
                // Result is observed!
                return;
            }

            // Making an exception for 'Task<Task>' case.
            // For instance, the following code is totally fine: await Task.WhenAll(t1, t2);
            if (awaitOperation.Type.IsTaskLike(context.Compilation))
            {
                return;
            }

            var diagnostic = CreateAwaitDiagnostic(awaitOperation);
            context.ReportDiagnostic(diagnostic);
        }

        /// <summary>
        /// Determines if the method should be analyzed for unobserved results.
        /// </summary>
        protected abstract bool ShouldAnalyzeMethod(IMethodSymbol method, Compilation compilation);

        /// <summary>
        /// Determines if the awaited result should be analyzed for being unobserved.
        /// </summary>
        protected virtual bool ShouldAnalyzeAwaitedResult(ITypeSymbol type, IMethodSymbol? method, Compilation compilation)
        {
            // By default, use the same logic as for regular method calls
            return method != null && ShouldAnalyzeMethod(method, compilation);
        }

        /// <summary>
        /// Creates a diagnostic for an unobserved method invocation.
        /// </summary>
        protected abstract Diagnostic CreateDiagnostic(IInvocationOperation invocation);

        /// <summary>
        /// Creates a diagnostic for an unobserved await expression.
        /// </summary>
        protected abstract Diagnostic CreateAwaitDiagnostic(IAwaitOperation awaitOperation);

        protected static bool ResultObservedByExtensionMethod(IInvocationOperation operation, Compilation compilation)
        {
            // In some cases, the following pattern is used:
            // Foo().Handle();
            // Where Foo() returns 'possible' that is passed to 'Handle' extension method that returns the result.
            // But in this case we can safely assume that the result WAS observed.

            var methodSymbol = operation.TargetMethod;

            // Exception for this rule is 'ConfigureAwait()'
            if (operation.TargetMethod.IsConfigureAwait(compilation))
            {
                return false;
            }

            // First, checking that method that is called is an extension method that takes the result.
            if (methodSymbol.IsExtensionMethod &&
                (methodSymbol.ReturnType.Equals(methodSymbol.ReceiverType, SymbolEqualityComparer.Default) ||
                 methodSymbol.ReturnType.Equals(methodSymbol.Parameters.FirstOrDefault()?.Type, SymbolEqualityComparer.Default)))
            {
                // operation.Type returns a type for 'Foo()'.
                return operation.Type?.Equals(methodSymbol.ReturnType, SymbolEqualityComparer.Default) == true;
            }

            return false;
        }

        protected static bool IsException(ITypeSymbol? type, Compilation compilation)
        {
            return type?.EnumerateBaseTypesAndSelf().Any(t => t.IsClrType(compilation, typeof(Exception))) == true;
        }
    }
}
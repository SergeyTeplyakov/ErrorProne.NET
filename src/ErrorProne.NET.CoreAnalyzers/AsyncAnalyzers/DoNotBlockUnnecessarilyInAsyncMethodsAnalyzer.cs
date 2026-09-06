using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using ErrorProne.NET.Core;
using ErrorProne.NET.CoreAnalyzers;

namespace ErrorProne.NET.AsyncAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DoNotBlockUnnecessarilyInAsyncMethodsAnalyzer : DiagnosticAnalyzerBase
    {
        public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptors.EPC35;

        /// <nodoc />
        public DoNotBlockUnnecessarilyInAsyncMethodsAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
            context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
        }

        private void AnalyzePropertyReference(OperationAnalysisContext context)
        {
            var propertyReference = (IPropertyReferenceOperation)context.Operation;

            var methodSymbol = propertyReference.FindEnclosingMethodSymbol(context);
            if (methodSymbol == null || !methodSymbol.IsAsync)
            {
                return;
            }

            if (propertyReference.Property.Name == "Result" &&
                propertyReference.Instance?.Type?.IsTaskLike(context.Compilation) == true)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, propertyReference.Syntax.GetLocation(), propertyReference.Syntax.ToString()));
            }
        }

        private void AnalyzeInvocation(OperationAnalysisContext context)
        {
            var invocation = (IInvocationOperation)context.Operation;

            var methodSymbol = invocation.FindEnclosingMethodSymbol(context);
            if (methodSymbol == null || !methodSymbol.IsAsync)
            {
                return;
            }

            var targetMethod = invocation.TargetMethod;

            if (targetMethod.Name == "Wait" && invocation.Instance?.Type?.IsTaskLike(context.Compilation) == true)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Syntax.GetLocation(), invocation.Syntax.ToString()));
            }
            else if (targetMethod.Name == "GetResult")
            {
                // Check for GetAwaiter().GetResult()
                if (invocation.Instance is IInvocationOperation getAwaiterInvocation &&
                    getAwaiterInvocation.TargetMethod.Name == "GetAwaiter" &&
                    getAwaiterInvocation.Instance?.Type.IsTaskLike(context.Compilation) == true)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Syntax.GetLocation(), invocation.Syntax.ToString()));
                }
            }
        }
    }
}

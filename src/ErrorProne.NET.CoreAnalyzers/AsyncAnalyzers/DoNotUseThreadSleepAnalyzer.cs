using System.Collections.Immutable;
using System.Threading;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.AsyncAnalyzers
{
    /// <summary>
    /// Analyzer that warns when Thread.Sleep is used in async methods.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DoNotUseThreadSleepAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = DiagnosticDescriptors.EPC33;

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        }

        private static void AnalyzeInvocation(OperationAnalysisContext context)
        {
            var invocation = (IInvocationOperation)context.Operation;
            
            // Check if this is Thread.Sleep call
            if (!IsThreadSleepCall(invocation, context.Compilation))
            {
                return;
            }

            // Check if we're inside an async method
            if (IsInAsyncMethod(invocation, context))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Syntax.GetLocation()));
            }
        }

        private static bool IsThreadSleepCall(IInvocationOperation invocation, Compilation compilation)
        {
            var targetMethod = invocation.TargetMethod;
            return targetMethod.ReceiverType.IsClrType(compilation, typeof(Thread)) && targetMethod.Name == nameof(Thread.Sleep);
        }

        private static bool IsInAsyncMethod(IOperation operation, OperationAnalysisContext context)
        {
            return operation.FindEnclosingMethodSymbol(context)?.IsAsync == true;
        }
    }
}

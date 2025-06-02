using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.CoreAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RecursiveCallAnalyzer : DiagnosticAnalyzer
    {
        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [DiagnosticDescriptors.EPC30];

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterOperationAction(AnalyzeMethodBody, OperationKind.MethodBody);
        }

        private static void AnalyzeMethodBody(OperationAnalysisContext context)
        {
            var method = (IMethodSymbol)context.ContainingSymbol;
            var methodBody = (IMethodBodyOperation)context.Operation;
            foreach (var invocation in methodBody.Descendants().OfType<IInvocationOperation>())
            {
                if (SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.OriginalDefinition, method.OriginalDefinition))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.EPC30,
                        invocation.Syntax.GetLocation(),
                        method.Name));
                }
            }
        }
    }
}

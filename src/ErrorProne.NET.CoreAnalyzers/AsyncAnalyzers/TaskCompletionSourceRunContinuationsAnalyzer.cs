using System.Collections.Immutable;
using System.Threading.Tasks;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.AsyncAnalyzers
{
    /// <summary>
    /// Analyzer that warns when TaskCompletionSource is created without TaskCreationOptions.RunContinuationsAsynchronously.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class TaskCompletionSourceRunContinuationsAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = DiagnosticDescriptors.EPC31;

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
        }

        private static void AnalyzeObjectCreation(OperationAnalysisContext context)
        {
            var objectCreation = (IObjectCreationOperation)context.Operation;
            var type = objectCreation.Constructor?.ContainingType;
            if (type == null)
            {
                return;
            }

            if (type.IsTaskCompletionSource(context.Compilation))
            {
                // Check if any argument is TaskCreationOptions.RunContinuationsAsynchronously
                bool hasRunContinuations = false;
                foreach (var arg in objectCreation.Arguments)
                {
                    var constant = arg.Value.ConstantValue;
                    if (constant.HasValue && constant.Value is int intValue)
                    {
                        int runContinuationsValue = (int)TaskCreationOptions.RunContinuationsAsynchronously;
                        if ((intValue & runContinuationsValue) == runContinuationsValue)
                        {
                            hasRunContinuations = true;
                            break;
                        }
                    }
                }
                if (!hasRunContinuations)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, objectCreation.Syntax.GetLocation()));
                }
            }
        }
    }
}

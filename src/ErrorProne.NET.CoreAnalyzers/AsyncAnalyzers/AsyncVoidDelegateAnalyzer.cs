using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.AsyncAnalyzers
{
    /// <summary>
    /// Warns when the async delegates are used as void-returning delegate.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AsyncVoidDelegateAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public static string DiagnosticId => Rule.Id;

        /// <nodoc />
        public static DiagnosticDescriptor Rule => DiagnosticDescriptors.EPC17;

        /// <nodoc />
        public AsyncVoidDelegateAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterOperationAction(AnalyzerOperation, OperationKind.AnonymousFunction);
        }

        private void AnalyzerOperation(OperationAnalysisContext context)
        {
            var anonymousFunction = (IAnonymousFunctionOperation) context.Operation;
            if (anonymousFunction.Symbol.IsAsync && anonymousFunction.Symbol.ReturnsVoid)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, anonymousFunction.Syntax.GetLocation()));
            }
        }
    }
}
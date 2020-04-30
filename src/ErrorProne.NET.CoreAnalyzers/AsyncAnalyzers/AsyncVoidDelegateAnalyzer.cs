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
        public const string DiagnosticId = DiagnosticIds.AsyncVoidDelegate;

        private const string Title = "Avoid async-void delegates";

        private const string Description = "Async-void delegates can cause application to crash.";
        private const string Category = "CodeSmell";

        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        public static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, Title, Category, Severity, isEnabledByDefault: true, description: Description);

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
                context.ReportDiagnostic(Diagnostic.Create(Descriptor, anonymousFunction.Syntax.GetLocation()));
            }
        }
    }
}
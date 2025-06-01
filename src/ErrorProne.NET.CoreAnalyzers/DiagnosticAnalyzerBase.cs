using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// Base class of diagnostic analyzers.
    /// </summary>
    public abstract class DiagnosticAnalyzerBase : DiagnosticAnalyzer
    {
        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }

        /// <summary>
        /// True if the analyzer should be used for generated code as well.
        /// </summary>
        public virtual bool ReportDiagnosticsOnGeneratedCode { get; } = true;

        /// <nodoc />
        protected DiagnosticAnalyzerBase(DiagnosticDescriptor descriptor)
        {
            SupportedDiagnostics = ImmutableArray.Create(descriptor);
        }
        
        /// <nodoc />
        protected DiagnosticAnalyzerBase(params DiagnosticDescriptor[] diagnostics)
        {
            SupportedDiagnostics = ImmutableArray.Create(diagnostics);
        }

        /// <inheritdoc />
        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            
            if (ReportDiagnosticsOnGeneratedCode)
            {
                context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            }

            InitializeCore(context);
        }

        /// <nodoc />
        protected abstract void InitializeCore(AnalysisContext context);
    }
}
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// Analyzer that warns about potential issues with respect to resource management.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ResourceLifetimeAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.ReturnIteratorFromUsingBlock;

        private static readonly string Title = "Suspicious resource management: lazy sequence is returned from a using block.";
        private static readonly string MessageFormat = "Disposable instance '{0}' may be used by a lazy iterator provided by '{1}' after dispose is called.";
        private static readonly string Description = "Free object may be used.";

        private const string Category = "CodeSmell";

        // Using warning for visibility purposes
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        private static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, Title, Category, Severity, isEnabledByDefault: true, description: Description);

        /// <nodoc />
        public ResourceLifetimeAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            
        }
    }
}
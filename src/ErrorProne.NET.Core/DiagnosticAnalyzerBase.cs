using System.Collections.Immutable;
using System.Diagnostics.ContractsLight;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// Base class of diagnostic analyzers.
    /// </summary>
    public abstract class DiagnosticAnalyzerBase : DiagnosticAnalyzer
    {
        /// <nodoc />
        protected readonly string DescriptorId;

        /// <nodoc />
        protected readonly DiagnosticDescriptor Descriptor;

        /// <summary>
        /// Diagnostic descriptor for fading code.
        /// </summary>
        protected readonly DiagnosticDescriptor? UnnecessaryWithSuggestionDescriptor;

        /// <nodoc />
        protected readonly DiagnosticDescriptor? UnnecessaryWithoutSuggestionDescriptor;

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }

        /// <nodoc />
        protected DiagnosticAnalyzerBase(
            DiagnosticDescriptor descriptor, params DiagnosticDescriptor[] diagnostics)
        : this(supportFading: false, new []{descriptor}.Concat(diagnostics).ToArray())
        {
        }
        
        /// <nodoc />
        protected DiagnosticAnalyzerBase(
            bool supportFading,
            params DiagnosticDescriptor[] diagnostics)
        {
            Contract.Requires(diagnostics.Length != 0);

            Descriptor = diagnostics[0];
            DescriptorId = Descriptor.Id;
            var supportedDiagnostics = diagnostics.ToImmutableArray();
            if (supportFading)
            {
                UnnecessaryWithSuggestionDescriptor = CreateUnnecessaryDescriptor();
                UnnecessaryWithoutSuggestionDescriptor = CreateUnnecessaryDescriptor(DescriptorId + "WithoutSuggestion");
                supportedDiagnostics = supportedDiagnostics.Add(UnnecessaryWithoutSuggestionDescriptor).Add(UnnecessaryWithSuggestionDescriptor);
            }

            SupportedDiagnostics = supportedDiagnostics;
        }

        /// <nodoc />
        protected DiagnosticDescriptor CreateDescriptorWithId(
            string id,
            LocalizableString title,
            LocalizableString messageFormat,
            params string[] customTags)
        {
            return new DiagnosticDescriptor(
                id, 
                title, 
                messageFormat,
                "Style",
                DiagnosticSeverity.Hidden,
                isEnabledByDefault: true,
                customTags: customTags);
        }

        /// <nodoc />
        protected DiagnosticDescriptor CreateUnnecessaryDescriptor()
            => CreateUnnecessaryDescriptor(DescriptorId);

        /// <nodoc />
        protected DiagnosticDescriptor CreateUnnecessaryDescriptor(string descriptorId)
            => CreateDescriptorWithId(
                descriptorId, "foo", "bar",
                //descriptorId, _localizableTitle, _localizableMessageFormat,
                WellKnownDiagnosticTags.Unnecessary);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            InitializeCore(context);
        }

        protected abstract void InitializeCore(AnalysisContext context);
    }
}
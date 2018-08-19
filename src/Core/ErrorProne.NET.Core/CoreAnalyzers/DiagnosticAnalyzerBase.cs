using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// Base class of a diagnostic analyzer.
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
        protected readonly DiagnosticDescriptor UnnecessaryWithSuggestionDescriptor;

        /// <nodoc />
        protected readonly DiagnosticDescriptor UnnecessaryWithoutSuggestionDescriptor;

        private readonly string _localizableTitle;
        private readonly string _localizableMessageFormat;

        /// <inheritdoc />
        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }

        /// <nodoc />
        protected DiagnosticDescriptor CreateUnnecessaryDescriptor()
            => CreateUnnecessaryDescriptor(DescriptorId);

        /// <nodoc />
        protected DiagnosticDescriptor CreateUnnecessaryDescriptor(string descriptorId)
            => CreateDescriptorWithId(
                descriptorId, _localizableTitle, _localizableMessageFormat,
                WellKnownDiagnosticTags.Unnecessary);

        /// <nodoc />
        protected DiagnosticAnalyzerBase(
            string descriptorId,
            string title,
            string messageFormat = null)
        {
            DescriptorId = descriptorId;
            _localizableTitle = title;
            _localizableMessageFormat = messageFormat ?? title;

            Descriptor = CreateDescriptor();
            UnnecessaryWithSuggestionDescriptor = CreateUnnecessaryDescriptor();
            UnnecessaryWithoutSuggestionDescriptor = CreateUnnecessaryDescriptor(descriptorId + "WithoutSuggestion");

            SupportedDiagnostics = ImmutableArray.Create(
                Descriptor, UnnecessaryWithoutSuggestionDescriptor, UnnecessaryWithSuggestionDescriptor);
        }
        
        /// <nodoc />
        protected DiagnosticAnalyzerBase(
            params DiagnosticDescriptor[] diagnostics)
        {
            Contract.Requires(diagnostics != null);
            Contract.Requires(diagnostics.Length != 0);

            Descriptor = diagnostics[0];
            DescriptorId = Descriptor.Id;
            UnnecessaryWithSuggestionDescriptor = CreateUnnecessaryDescriptor();
            UnnecessaryWithoutSuggestionDescriptor = CreateUnnecessaryDescriptor(DescriptorId + "WithoutSuggestion");

            SupportedDiagnostics = ImmutableArray.Create(
                Descriptor, UnnecessaryWithoutSuggestionDescriptor, UnnecessaryWithSuggestionDescriptor);
        }

        /// <nodoc />
        protected DiagnosticDescriptor CreateDescriptor(params string[] customTags)
            => CreateDescriptorWithId(DescriptorId, _localizableTitle, _localizableMessageFormat, customTags);

        /// <nodoc />
        protected DiagnosticDescriptor CreateDescriptorWithId(
            string id, LocalizableString title, LocalizableString messageFormat,
            params string[] customTags)
        {
            return new DiagnosticDescriptor(
                id, title, messageFormat,
                "Style",
                DiagnosticSeverity.Hidden,
                isEnabledByDefault: true,
                customTags: customTags);
        }
    }
}
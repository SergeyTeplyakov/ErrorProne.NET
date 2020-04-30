using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.CoreAnalyzers.NonDefaultStructs
{
    /// <summary>
    /// An analyzer warns when a struct with non-default invariants is constructed via default construction.
    /// For instance <code>ImmutableArray&lt;int&gt; a = default; int x = a.Count; will fail with NRE.</code>
    /// </summary>
    /// <remarks>
    /// Technically this analyzer belongs to StructAnalyzers project, but because its more important
    /// and a bit more common, I decided to put it into the set of core analyzers.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DoNotCreateStructWithNoDefaultStructConstructionAttributeAnalyzer : DefaultStructConstructionAnalyzerBase
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.DoNotUseDefaultConstructionForStruct;

        private static readonly string Title = $"Do not use default construction for a struct marked with `{DoNotUseDefaultConstructionAttributeName}`.";

        private static readonly string Message =
            $"Do not use default construction for a struct '{{0}}' marked with `{DoNotUseDefaultConstructionAttributeName}`.{{1}}";
        private static readonly string Description = $"Structs marked with `{DoNotUseDefaultConstructionAttributeName}` should be " +
                                                     "constructed using a non-default constructor.";
        
        private const string Category = "CodeSmell";

        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        private static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, Message, Category, Severity, isEnabledByDefault: true, description: Description);

        /// <nodoc />
        public DoNotCreateStructWithNoDefaultStructConstructionAttributeAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
            context.RegisterOperationAction(AnalyzeDefaultValue, OperationKind.DefaultValue);
        }

        private void AnalyzeDefaultValue(OperationAnalysisContext context)
        {
            var operation = (IDefaultValueOperation)context.Operation;
            ReportDiagnosticForTypeIfNeeded(context.Compilation, operation.Syntax, operation.Type, Rule, context.ReportDiagnostic);
        }

        private void AnalyzeObjectCreation(OperationAnalysisContext context)
        {
            var operation = (IObjectCreationOperation)context.Operation;
            ReportDiagnosticForTypeIfNeeded(context.Compilation, operation.Syntax, operation.Type, Rule, context.ReportDiagnostic);
        }
    }
}
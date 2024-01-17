using System.Collections.Immutable;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// An analyzer that warns when a struct with the default implementation of <see cref="object.Equals(object)"/> or <see cref="object.GetHashCode()"/> is used anywhere.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DefaultEqualsOrHashCodeUsageAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public static DiagnosticDescriptor Rule => DiagnosticDescriptors.EPC25;
        
        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        }

        private void AnalyzeInvocation(OperationAnalysisContext context)
        {
            var methodCall = (IInvocationOperation)context.Operation;
            if (methodCall.Instance?.Type is not null &&
                methodCall.Instance.Type.IsStruct() &&
                (methodCall.TargetMethod.Name == nameof(Equals) || methodCall.TargetMethod.Name == nameof(GetHashCode)) &&
                methodCall.TargetMethod.ContainingType.SpecialType is SpecialType.System_ValueType)
            {
                string equalsOrHashCodeAsString = methodCall.TargetMethod.Name;
                var diagnostic = Diagnostic.Create(Rule, methodCall.Syntax.GetLocation(), equalsOrHashCodeAsString, $"{methodCall.Syntax.ToFullString()}");
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
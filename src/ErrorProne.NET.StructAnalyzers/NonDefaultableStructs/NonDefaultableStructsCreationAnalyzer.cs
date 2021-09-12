using ErrorProne.NET.StructAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.Net.StructAnalyzers.NonDefaultStructs
{
    /// <summary>
    /// An analyzer warns when a struct with non-default invariants is constructed via default construction.
    /// For instance <code>ImmutableArray&lt;int&gt; a = default; int x = a.Count; will fail with NRE.</code>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NonDefaultableStructsCreationAnalyzer : NonDefaultableStructAnalyzerBase
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.DoNotUseDefaultConstructionForStruct;

        private const string Title = "Do not construct non-defaultable struct with 'default' expression.";

        private const string Message =
            "Do not construct non-defaultable struct '{0}' with 'deault' expression.{1}";
        private const string Description = "Non-defaultable structs can not be constructed using the default constructor or default(T) expression.";
        
        private const string Category = "CodeSmell";

        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        private static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, Message, Category, Severity, isEnabledByDefault: true, description: Description);

        /// <nodoc />
        public NonDefaultableStructsCreationAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
            context.RegisterOperationAction(AnalyzeDefaultValue, OperationKind.DefaultValue);
            context.RegisterOperationAction(AnalyzeMethodInvocation, OperationKind.Invocation);
        }

        private void AnalyzeMethodInvocation(OperationAnalysisContext context)
        {
            var operation = (IInvocationOperation) context.Operation;
            
            // Searching for cases like Create<MyStruct>() when the generic has new T() constraint.
            if (operation.TargetMethod.IsGenericMethod)
            {
                var constructedFrom = operation.TargetMethod.ConstructedFrom;

                // Need to find only when the return type has new T() constraint.
                if (constructedFrom.ReturnType.TypeKind == TypeKind.TypeParameter &&
                    constructedFrom.ReturnType is ITypeParameterSymbol tps && tps.HasConstructorConstraint)
                {
                    ReportDiagnosticForTypeIfNeeded(context.Compilation, operation.Syntax, operation.TargetMethod.ReturnType, Rule, context.ReportDiagnostic);
                }

                // Another case: out or ref parameters with new T() constraint.
                foreach (var p in operation.TargetMethod.Parameters)
                {
                    if (p.OriginalDefinition.Type.TypeKind == TypeKind.TypeParameter
                        && (p.OriginalDefinition.RefKind == RefKind.Out || p.OriginalDefinition.RefKind == RefKind.Ref))
                    {
                        // Need to warn even without 'new T()' constraint, because a method can modify the argument by
                        // doing 't = default;'
                        ReportDiagnosticForTypeIfNeeded(context.Compilation, operation.Syntax, p.Type, Rule, context.ReportDiagnostic);
                    }
                }
            }
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
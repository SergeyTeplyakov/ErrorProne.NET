using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.AsyncAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NullConditionalOperatorAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public static string DiagnosticId => Rule.Id;

        /// <nodoc />
        public static DiagnosticDescriptor Rule => DiagnosticDescriptors.EPC16;

        /// <nodoc />
        public NullConditionalOperatorAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterOperationAction(AnalyzeAwaitOperation, OperationKind.Await);
            //context.RegisterSyntaxNodeAction(AnalyzeAwaitExpression, SyntaxKind.AwaitExpression);
        }

        private void AnalyzeAwaitOperation(OperationAnalysisContext context)
        {
            var operation = (IAwaitOperation) context.Operation;
            // Check if the awaited operation is a conditional access operation
            if (operation.Operation is IConditionalAccessOperation)
            {
                if (operation.Operation.Type?.NullableAnnotation == NullableAnnotation.Annotated)
                {
                    // If the type is annotated, we don't report diagnostics.
                    return;
                }

                var location = operation.Syntax.GetLocation();
                var diagnostic = Diagnostic.Create(Rule, location);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void AnalyzeAwaitExpression(SyntaxNodeAnalysisContext context)
        {
            var invocation = (AwaitExpressionSyntax)context.Node;

            if (invocation.Expression is ConditionalAccessExpressionSyntax)
            {
                var location = invocation.GetLocation();
                var diagnostic = Diagnostic.Create(Rule, location);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
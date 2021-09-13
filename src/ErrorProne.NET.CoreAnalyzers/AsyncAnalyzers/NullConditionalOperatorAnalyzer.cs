using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

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
            context.RegisterSyntaxNodeAction(AnalyzeAwaitExpression, SyntaxKind.AwaitExpression);
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
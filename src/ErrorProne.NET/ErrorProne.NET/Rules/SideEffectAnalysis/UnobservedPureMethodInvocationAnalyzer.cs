using System.Collections.Immutable;
using ErrorProne.NET.Annotations;
using ErrorProne.NET.Common;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Rules.SideEffectAnalysis
{
    /// <summary>
    /// This could be a relatively tough rule but very helpful.
    /// Few heuristics:
    /// - method with Pure attribute considered pure
    /// - method with <see cref="UseReturnValueAttribute"/>
    /// - method that returns IEnumerable, that is static and is an extension method considered pure
    /// - methods from Roslyn API are known to be pure
    /// - methods from the type that marked as [Immutable] considered pure
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnobservedPureMethodInvocationAnalyzer : DiagnosticAnalyzer
    {
        private readonly string _s;
        public string S { get; }
        public const string DiagnosticId = RuleIds.UnobservedPureMethodInvocationId;

        private static readonly string Title = "Non-observed return value of the pure method.";
        // candidates:
        // Unobserved invocation of pure method '{0}'
        // Avoid unobserved invocation of the pure method '{0}'

        // for UseReturnValueAttribute
        // Unobserved invocation of the method '{0}' marked with 'UseReturnValueAttribute'
        private static readonly string Message = "Non-observed return value of the pure method.";
        private static readonly string Description = "Return value of pure method should be observed.";

        private const string Category = "Bug";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, Message, Category,
            DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeMethodInvocation, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeMethodInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax) context.Node;

            // TODO: different error message should be used for UseReturnValueAttribute
            if (invocation.Parent is ExpressionStatementSyntax && invocation.IsPure(context.SemanticModel))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetNodeLocationForDiagnostic()));
            }
        } 
    }
}
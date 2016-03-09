using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Common;
using ErrorProne.NET.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Rules.ExceptionHandling
{
    /// <summary>
    /// Analyzer that warns if async method has synchronous (i.e. suspicious) precondition check.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AsyncMethodPreconditionsAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = RuleIds.SuspiciousPreconditionInAsyncMethod;

        private const string Title = "Non-synchronous precondition in async method";
        private const string MessageFormat = "Method {0} has non-synchronous precondition";
        private const string Description = "Exceptions in async method will lead to failed task and would not be thrown synchrnously to the client of the code";
        private const string Category = "CodeSmell";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var method = (MethodDeclarationSyntax) context.Node;

            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method);

            if (methodSymbol == null || !methodSymbol.IsAsync) return;
                
            var contractBlock = PreconditionsBlock.GetPreconditions(method, context.SemanticModel);

            foreach (var s in contractBlock.Preconditions)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, s.ThrowStatement.GetLocation(), methodSymbol.Name));
            }
        }
    }
}

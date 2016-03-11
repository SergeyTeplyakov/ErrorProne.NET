using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Common;
using ErrorProne.NET.Extensions;
using ErrorProne.NET.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Rules.ExceptionHandling
{
    /// <summary>
    /// Analyzer that warns if iterator block has synchronous (i.e. suspicious) precondition check.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class IteratorBlockPreconditionsAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = RuleIds.SuspiciousPreconditionInIteratorBlock;

        private const string Title = "Non-synchronous precondition in iterator block";
        private const string MessageFormat = "Method '{0}' has non-synchronous precondition";
        private const string Description = "Precondition would be lazily evaluated and excpetion will occurred only with a first call to MoveNext method.";
        private const string Category = "CodeSmell";

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var method = (MethodDeclarationSyntax) context.Node;

            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method);

            if (methodSymbol == null || !method.IsIteratorBlock()) return;
                
            var contractBlock = PreconditionsBlock.GetPreconditions(method, context.SemanticModel);

            foreach (var s in contractBlock.Preconditions)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, s.ThrowStatement.GetLocation(), methodSymbol.Name));
            }
        }
    }
}

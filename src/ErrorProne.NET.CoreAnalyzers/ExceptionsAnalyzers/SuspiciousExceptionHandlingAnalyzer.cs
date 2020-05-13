using System.Diagnostics.ContractsLight;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// Analyzer for suspicious or incorrect exception handling.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class SuspiciousExceptionHandlingAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc /> // used by tests
        public static readonly DiagnosticDescriptor DiagnosticDescriptor = new DiagnosticDescriptor(id: DiagnosticId, title: Title, messageFormat: MessageFormat,
            category: Category, defaultSeverity: Severity, description: Description, isEnabledByDefault: true);

        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.SuspiciousExceptionHandling;

        private const string Title =
            "Suspicious exception handling: only the 'Message' property is observed in the catch block.";

        private const string MessageFormat =
            "Suspicious exception handling: only '{0}.Message' is observed in the catch block.";

        private const string Description =
            "In many cases the 'Message' property contains irrelevant information and actual data is kept in inner exceptions.";

        private const string Category = "CodeSmell";

        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        public SuspiciousExceptionHandlingAnalyzer()
            : base(supportFading: true, DiagnosticDescriptor)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            // I don't know why yet, but selecting SytaxKind.CatchClause lead to very strange behavior:
            // AnalyzeSyntax method would called for a few times and the same warning would be added to diagnostic list!
            // Using IdentifierName syntax instead.
            context.RegisterSyntaxNodeAction(AnalyzeCatchBlock, SyntaxKind.CatchClause);
        }

        // Called when Roslyn encounters a catch clause.
        private void AnalyzeCatchBlock(SyntaxNodeAnalysisContext context)
        {
            var catchBlock = (CatchClauseSyntax)context.Node;

            if (catchBlock.Declaration != null && catchBlock.Declaration.CatchIsTooGeneric(context.SemanticModel))
            {
                var usages = context.SemanticModel.GetExceptionIdentifierUsages(catchBlock);
                if (usages.Count == 0)
                {
                    // Exception was not observed. Warning would be emitted by different rule
                    return;
                }

                // First of all we should find all usages for ex.Message
                var messageUsages = usages
                    .Select(id =>
                        new { Parent = id.Identifier.Parent as MemberAccessExpressionSyntax, Id = id.Identifier })
                    .Where(x => x.Parent != null && x.Parent.Name.GetText().ToString() == "Message")
                    .ToList();

                if (messageUsages.Count == 0)
                {
                    // There would be no warnings! No ex.Message usages 
                    return;
                }

                bool wasObserved =
                    usages.Select(id => id.Identifier)
                        .Except(messageUsages.Select(x => x.Id))
                        .Any(u => u.Parent is ArgumentSyntax || // Exception object was used directly
                                  u.Parent is AssignmentExpressionSyntax || // Was saved to field or local
                                                                            // or Inner exception was used
                                  ((u.Parent as MemberAccessExpressionSyntax)?.Name?.Identifier)?.Text ==
                                  "InnerException");

                // If exception object was "observed" properly!
                if (wasObserved)
                {
                    return;
                }

                foreach (var messageUsage in messageUsages)
                {
                    // "Fading" .Message property usage.
                    var parent = messageUsage.Parent;
                    Contract.Assert(parent != null);
                    Contract.Assert(messageUsage.Parent != null);

                    var fadingSpan = Location.Create(context.Node.SyntaxTree,
                        TextSpan.FromBounds(parent.Name.Span.Start, messageUsage.Parent.Name.Span.End));

                    var location = messageUsage.Id.GetLocation();

                    context.ReportDiagnostic(
                        Diagnostic.Create(DiagnosticDescriptor, location, messageUsage.Id.Identifier.Text));

                    context.ReportDiagnostic(
                        Diagnostic.Create(UnnecessaryWithSuggestionDescriptor!, fadingSpan));
                }
            }
        }
    }
}
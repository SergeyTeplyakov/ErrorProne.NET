using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using ErrorProne.NET.Common;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.SwitchAnalysis
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MissingCasesInSwitchStatementAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = RuleIds.MissingCasesInSwitchStatement;

        private static readonly string Title = "Some enum cases are not covered by switch statement.";
        private static readonly string Message = "Potentially missed enum case(s) {0} in the switch statement.";
        private static readonly string Description = "Switch statement that throws in default doesn't cover some cases";

        private const string Category = "CodeSmell";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, Message, Category,
            DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeSwitchStatement, SyntaxKind.SwitchStatement);
        }

        private void AnalyzeSwitchStatement(SyntaxNodeAnalysisContext context)
        {
            var switchStatement = (SwitchStatementSyntax)context.Node;

            var switchAnalyzer = new SwitchAnalyzer(switchStatement, context.SemanticModel);

            if (!switchAnalyzer.SwitchOverEnum)
            {
                return;
            }

            if (!DefaultSectionThrows(switchStatement, context.SemanticModel))
            {
                return;
            }

            var enumValues = switchAnalyzer.SortedEnumFIeldsAndValues;
            var enums = switchAnalyzer.SortedEnumFIeldsAndValues.ToDictionarySafe(e => e.Item1, e => e.Item2);

            var cases = switchAnalyzer.Cases;
            var uniqueCases = new HashSet<long>(cases.Select(c => c.Item2));

            var nonCoveredValues =
                enumValues
                    .ToDictionarySafe(e => e.Item2, e => e.Item1)
                    .Where(kvp => !uniqueCases.Contains(kvp.Key))
                    .ToList();

            if (nonCoveredValues.Count != 0)
            {
                string message = string.Join(",", nonCoveredValues.Select(kvp => $"'{kvp.Value.Name}'"));

                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, switchStatement.SwitchKeyword.GetLocation(), message));
            }
        }

        private bool DefaultSectionThrows(SwitchStatementSyntax switchStatement, SemanticModel semanticModel)
        {
            var defaultCase = switchStatement.Sections.FirstOrDefault(s => s.Labels.OfType<DefaultSwitchLabelSyntax>().Any());

            if (defaultCase == null)
            {
                return false;
            }

            bool throwsInvalidOperation =
                defaultCase.Statements
                    // Filtering throw
                    .OfType<ThrowStatementSyntax>()
                    .Select(s => s.Expression)
                    // Filtering throw new Exception
                    .OfType<ObjectCreationExpressionSyntax>()
                    // Filtering unknown symbols
                    .Select(s => semanticModel.GetSymbolInfo(s.Type).Symbol)
                    .Where(s => s != null)
                    // True, if throw new InvalidOperationException()
                    .Any(s => s.Equals(semanticModel.GetClrType(typeof(InvalidOperationException))));

            if (throwsInvalidOperation)
            {
                return true;
            }

            bool hasContractAssertOrAssumeWithFalse =
                defaultCase.Statements
                    // Getting only expressions
                    .OfType<ExpressionStatementSyntax>()
                    // that calls functions
                    .Select(s => s.Expression as InvocationExpressionSyntax)
                    .Where(s => s != null)
                    // with first argument equals to false
                    .Where(s =>
                            s.ArgumentList.Arguments.FirstOrDefault()?.Expression?.Kind() == SyntaxKind.FalseLiteralExpression)
                    // with known symbols
                    .Select(s => semanticModel.GetSymbolInfo(s).Symbol as IMethodSymbol)
                    .Where(s => s != null)
                    // Contract.Assert/Assume or Debug.Assert
                    .Any(m => (m.ContainingType.Equals(semanticModel.GetClrType(typeof(Contract))) &&
                               (m.Name == "Assert" || m.Name == "Assume")) ||
                              (m.ContainingType.Name == "Debug" && m.Name == "Assert"));

            if (hasContractAssertOrAssumeWithFalse)
            {
                return true;
            }

            return false;
        }
    }
}
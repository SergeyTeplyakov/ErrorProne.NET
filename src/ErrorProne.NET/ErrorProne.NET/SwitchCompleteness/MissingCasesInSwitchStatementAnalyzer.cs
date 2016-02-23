using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Reflection.Metadata;
using ErrorProne.NET.Common;
using ErrorProne.NET.Core;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.SideEffectRules
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
            var switchStatement = (SwitchStatementSyntax) context.Node;

            var expressionSymbol = context.SemanticModel.GetSymbolInfo(switchStatement.Expression).Symbol;
            var expressionType = GetSymbolType(expressionSymbol) as INamedTypeSymbol;

            if (!IsEnum(expressionType, context.SemanticModel))
            {
                return;
            }

            if (!DefaultSectionThrows(switchStatement, context.SemanticModel))
            {
                return;
            }

            var enumValues = expressionType.GetSortedEnumFieldsAndValues();
            var enums = enumValues.ToDictionarySafe(e => e.Item1, e => e.Item2);

            var cases = GetUsedCases(switchStatement, context.SemanticModel, enums);
            var uniqueCases = new HashSet<ulong>(cases);

            var nonCoveredValues =
                enumValues
                    .ToDictionarySafe(e => e.Item2, e => e.Item1)
                    .Where(kvp => !uniqueCases.Contains((ulong) kvp.Key))
                    .ToList();

            if (nonCoveredValues.Count != 0)
            {
                string message = string.Join(",", nonCoveredValues.Select(kvp => $"'{kvp.Value}'"));

                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, switchStatement.SwitchKeyword.GetLocation(), message));
            }

        }

        private static List<ulong> GetUsedCases(SwitchStatementSyntax switchStatement, SemanticModel semanticModel, Dictionary<IFieldSymbol, long> enums)
        {
            var cases = new List<ulong>();
            // Need to exclude default!
            var caseStatements = switchStatement.Sections.Select(s => s.Statements.First()).ToList();
            foreach (var s in caseStatements)
            {
                ulong? caseValue = null;

                var expression = s as ExpressionStatementSyntax;
                if (expression != null)
                {
                    // It could be a cast!
                    var castExpression = expression.Expression as CastExpressionSyntax;
                    // Can cover only if expression inside the cast is constant!
                    var literal = castExpression?.Expression as LiteralExpressionSyntax;
                    if (literal != null)
                    {
                        caseValue = Convert.ToUInt64(literal.Token.Value);
                    }
                    else
                    {
                        var symbol = semanticModel.GetSymbolInfo(expression.Expression).Symbol as IFieldSymbol;
                        if (symbol != null)
                        {
                            Contract.Assert(enums.ContainsKey(symbol), "Enum case should be present in enum field");
                            caseValue = (ulong) enums[symbol];
                        }
                    }
                }

                if (caseValue != null)
                {
                    cases.Add(caseValue.Value);
                }
            }
            return cases;
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
                    .Any(s => s.Equals(semanticModel.GetClrType(typeof (InvalidOperationException))));

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
                    .Any(m => (m.ContainingType.Equals(semanticModel.GetClrType(typeof (Contract))) && 
                               (m.Name == "Assert" || m.Name == "Assume")) ||
                              (m.ContainingType.Name == "Debug" && m.Name == "Assert"));

            if (hasContractAssertOrAssumeWithFalse)
            {
                return true;
            }

            return false;
        }

        private ITypeSymbol GetSymbolType(ISymbol symbol)
        {
            var parameterSymbol = symbol as IParameterSymbol;
            if (parameterSymbol != null) return parameterSymbol.Type;

            var local = symbol as ILocalSymbol;
            if (local != null) return local.Type;

            var field = symbol as IFieldSymbol;
            if (field != null) return field.Type;

            var method = symbol as IMethodSymbol;
            if (method != null) return method.ReturnType;

            var property = symbol as IPropertySymbol;
            if (property != null) return property.Type;

            return null;
        }

        private bool IsEnum(ITypeSymbol type, SemanticModel semanticModel)
        {
            return type?.IsValueType == true && type.BaseType.Equals(semanticModel.GetClrType(typeof (System.Enum)));
        }
    }
}
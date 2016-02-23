using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using ErrorProne.NET.Extensions;
using ErrorProne.NET.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.SwitchAnalysis
{
    /// <summary>
    /// Helper class that provides useful information regarding the switch statements.
    /// </summary>
    public sealed class SwitchAnalyzer
    {
        private readonly SwitchStatementSyntax _switchStatement;
        private readonly SemanticModel _semanticModel;

        private readonly Lazy<INamedTypeSymbol> _expressionType;

        private readonly Lazy<bool> _switchOverEnum;
        private readonly Lazy<ImmutableList<Tuple<IFieldSymbol, long>>> _enumValues;
        private readonly Lazy<ImmutableList<Tuple<ExpressionSyntax, long>>> _cases; 

        public SwitchAnalyzer(SwitchStatementSyntax switchStatement, SemanticModel semanticModel)
        {
            // Hm...! It seems that Code Contracts still has a bug, and uncommenting Contract.Requires lead to NRE!
            //Contract.Requires(switchStatement != null);
            //Contract.Requires(semanticModel != null);

            _switchStatement = switchStatement;
            _semanticModel = semanticModel;

            var expressionSymbol = semanticModel.GetSymbolInfo(switchStatement.Expression).Symbol;
            _expressionType = LazyEx.Create(() => GetSymbolType(expressionSymbol));
            _switchOverEnum = LazyEx.Create(() => _expressionType.Value.IsEnum(semanticModel));
            _enumValues = LazyEx.Create(() => _expressionType.Value.GetSortedEnumFieldsAndValues().ToImmutableList());
            _cases = LazyEx.Create(() => GetUsedCases().ToImmutableList());
        }

        public bool SwitchOverEnum => _switchOverEnum.Value;
        public ImmutableList<Tuple<IFieldSymbol, long>> SortedEnumFIeldsAndValues => _enumValues.Value;
        public ImmutableList<Tuple<ExpressionSyntax, long>> Cases => _cases.Value;

        [Pure]
        private INamedTypeSymbol GetSymbolType(ISymbol symbol)
        {
            var parameterSymbol = symbol as IParameterSymbol;
            if (parameterSymbol != null) return (INamedTypeSymbol)parameterSymbol.Type;

            var local = symbol as ILocalSymbol;
            if (local != null) return (INamedTypeSymbol)local.Type;

            var field = symbol as IFieldSymbol;
            if (field != null) return (INamedTypeSymbol)field.Type;

            var method = symbol as IMethodSymbol;
            if (method != null) return (INamedTypeSymbol)method.ReturnType;

            var property = symbol as IPropertySymbol;
            if (property != null) return (INamedTypeSymbol)property.Type;

            return null;
        }

        private List<Tuple<ExpressionSyntax, long>> GetUsedCases()
        {
            var cases = new List<Tuple<ExpressionSyntax, long>>();
            var enums = _enumValues.Value.ToDictionarySafe(e => e.Item1, e => e.Item2);

            // Need to exclude default!
            var caseExpressions = _switchStatement.Sections.SelectMany(s => s.Labels).OfType<CaseSwitchLabelSyntax>().Select(l => l.Value).ToList();
            foreach (var expression in caseExpressions)
            {
                long? caseValue = null;

                if (expression != null)
                {
                    // It could be a cast!
                    var castExpression = expression as CastExpressionSyntax;
                    // Can cover only if expression inside the cast is constant!
                    var literal = castExpression?.Expression as LiteralExpressionSyntax;
                    if (literal != null)
                    {
                        caseValue = Convert.ToInt64(literal.Token.Value);
                    }
                    else
                    {
                        var symbol = _semanticModel.GetSymbolInfo(expression).Symbol as IFieldSymbol;
                        if (symbol != null)
                        {
                            Contract.Assert(enums.ContainsKey(symbol), "Enum case should be present in enum field");
                            caseValue = enums[symbol];
                        }
                    }
                }

                if (caseValue != null)
                {
                    cases.Add(Tuple.Create(expression, caseValue.Value));
                }
            }

            return cases;
        }
    }
}
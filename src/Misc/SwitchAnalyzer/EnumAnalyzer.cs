using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SwitchAnalyzer
{
    public class EnumAnalyzer
    {
        private const string Category = "Correctness";

        public const string DiagnosticId = "EPW01";
        private const string Title = "Non exhaustive patterns in switch block";
        private const string MessageFormat = "Switch case should check enum value(s): {0}";
        private const string Description = "All enum cases in switch statement should be checked.";
        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticId, 
            title: Title, 
            messageFormat: MessageFormat, 
            category: Category, 
            defaultSeverity: DiagnosticSeverity.Warning, 
            isEnabledByDefault: true,
            description: Description);

        public static IEnumerable<SwitchArgumentTypeItem<T>> AllEnumValues<T>(ITypeSymbol expressionType)
        {
            var expressionTypeEnumName = expressionType.Name;
            var enumSymbols = expressionType.GetMembers().Where(x => x.Kind == SymbolKind.Field).OfType<IFieldSymbol>();
            var allEnumValues = enumSymbols.Select(x => new SwitchArgumentTypeItem<T>(
                prefix: x.ContainingNamespace.Name,
                member: expressionTypeEnumName,
                fullName: $"{expressionTypeEnumName}.{x.Name}", 
                value: (T) x.ConstantValue)).ToList();
            return allEnumValues;
        }

        public static bool ShouldProceedWithChecks(SyntaxList<SwitchSectionSyntax> caseSyntaxes)
        {
            return DefaultCaseCheck.ShouldProceedWithDefault(caseSyntaxes);
        }

        public static IEnumerable<string> CaseIdentifiers(SyntaxList<SwitchSectionSyntax> switchCases)
        {
            var caseExpressions = GetCaseExpressions(switchCases);
            var allCaseMembers = GetMemeberAccessExpressionSyntaxes(caseExpressions).ToList();
            var caseIdentifiers = allCaseMembers
                .Select(GetIdentifierAndName)
                .Where(x => x.identifier != null && x.name != null)
                .Select(x => $"{x.identifier}.{x.name}")
                .ToList();
            return caseIdentifiers;
        }

        private static IEnumerable<MemberAccessExpressionSyntax> GetMemeberAccessExpressionSyntaxes(IEnumerable<ExpressionSyntax> expressions)
        {
            return expressions.SelectMany(GetExpressions);

            IEnumerable<MemberAccessExpressionSyntax> GetExpressions(ExpressionSyntax expression)
            {
                if (expression is MemberAccessExpressionSyntax member)
                {
                    return new[] { member };
                }
                if (expression is ParenthesizedExpressionSyntax parenthesized)
                {
                    return GetExpressions(parenthesized.Expression);
                }
                if (expression is BinaryExpressionSyntax binaryExpression)
                {
                    if (binaryExpression.Kind() == SyntaxKind.BitwiseAndExpression)
                    {
                        var left = GetExpressions(binaryExpression.Left).ToList();
                        var right = GetExpressions(binaryExpression.Right).ToList();
                        if (AreExpressionListsEqual(left, right))
                        {
                            return left;
                        }
                        else
                        {
                            return new MemberAccessExpressionSyntax[0];
                        }
                    }
                    if (binaryExpression.Kind() == SyntaxKind.BitwiseOrExpression)
                    {
                        var left = GetExpressions(binaryExpression.Left);
                        var right = GetExpressions(binaryExpression.Right);
                        return left.Union(right);
                    }
                }
                return new MemberAccessExpressionSyntax[0];
            }
        }

        private static bool AreExpressionListsEqual(IList<MemberAccessExpressionSyntax> left, IList<MemberAccessExpressionSyntax> right)
        {
            var leftHasRightElements = left.All(l => right.Any(r => AreMemberAccessExpressionsEqual(l, r)));
            var rightHasLeftElements = right.All(r => left.Any(l => AreMemberAccessExpressionsEqual(l, r)));

            return leftHasRightElements && rightHasLeftElements;
        }

        private static bool AreMemberAccessExpressionsEqual(MemberAccessExpressionSyntax left, MemberAccessExpressionSyntax right)
        {
            var leftIdentifier = GetIdentifierAndName(left);
            var rightIdentifier = GetIdentifierAndName(right);
            return $"{leftIdentifier.identifier}.{leftIdentifier.name}" == $"{rightIdentifier.identifier}.{rightIdentifier.name}";
        }

        private static IEnumerable<ExpressionSyntax> GetCaseExpressions(IEnumerable<SwitchSectionSyntax> caseSyntaxes)
        {
            var caseSwitchSyntaxes = caseSyntaxes.Where(x => x.Labels.FirstOrDefault() is CaseSwitchLabelSyntax);
            var caseLabels = caseSwitchSyntaxes.SelectMany(x => x.Labels).OfType<CaseSwitchLabelSyntax>();
            var caseExpressions = caseLabels.Select(x => x.Value);
            return caseExpressions;
        }

        private static (string identifier, string name) GetIdentifierAndName(MemberAccessExpressionSyntax syntax)
        {
            var simpleAccess = syntax.ChildNodes().ToList();

            var enumValue = simpleAccess.LastOrDefault() as IdentifierNameSyntax;

            if (simpleAccess.FirstOrDefault() is IdentifierNameSyntax enumType && enumValue != null)
            {
                return (enumType.Identifier.Value.ToString(), enumValue.Identifier.Value.ToString());
            }

            if (simpleAccess.FirstOrDefault() is MemberAccessExpressionSyntax member && enumValue != null)
            {
                return (member.Name.Identifier.Value.ToString(), enumValue.Identifier.Value.ToString());
            }
            return (null, null);
        }
    }
}

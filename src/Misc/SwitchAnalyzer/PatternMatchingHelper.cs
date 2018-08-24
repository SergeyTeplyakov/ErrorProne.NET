using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace SwitchAnalyzer
{
    class PatternMatchingHelper
    {
        public static IEnumerable<string> GetCaseValues(IEnumerable<SwitchSectionSyntax> caseSyntaxes)
        {
            var caseExpressions = GetCaseDeclarationPatternSyntaxes(caseSyntaxes);
            var caseValues = caseExpressions.Select(x => x.Type)
                .OfType<IdentifierNameSyntax>()
                .Select(x => x.Identifier.Text);
            return caseValues;
        }

        private static IEnumerable<DeclarationPatternSyntax> GetCaseDeclarationPatternSyntaxes(IEnumerable<SwitchSectionSyntax> caseSyntaxes)
        {
            var caseSwitchSyntaxes = caseSyntaxes.Where(x => x.Labels.FirstOrDefault() is CasePatternSwitchLabelSyntax);
            var caseLabels = caseSwitchSyntaxes.SelectMany(x => x.Labels).OfType<CasePatternSwitchLabelSyntax>();
            var caseExpressions = caseLabels.Select(x => x.Pattern).OfType<DeclarationPatternSyntax>();
            return caseExpressions;
        }

        public static bool HasVarDeclaration(SyntaxList<SwitchSectionSyntax> caseSyntaxes)
        {
            var caseExpressions = GetCaseDeclarationPatternSyntaxes(caseSyntaxes);
            var varDeclaration = caseExpressions.Select(x => x.Type)
                .OfType<IdentifierNameSyntax>()
                .Select(x => x.Identifier.Text)
                .FirstOrDefault(x => x == "var");

            return varDeclaration != null;
        }
    }
}

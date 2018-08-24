using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SwitchAnalyzer
{
    class DefaultCaseCheck
    {

        public static bool ShouldProceedWithDefault(SyntaxList<SwitchSectionSyntax> caseSyntaxes)
        {
            var defaultCase = GetDefaultExpression(caseSyntaxes);
            return ShouldProcessWithDefault(defaultCase);
        }

        private static SwitchSectionSyntax GetDefaultExpression(SyntaxList<SwitchSectionSyntax> caseSyntaxes)
        {
            return caseSyntaxes.FirstOrDefault(x => x.Labels.FirstOrDefault() is DefaultSwitchLabelSyntax);
        }

        private static bool ShouldProcessWithDefault(SwitchSectionSyntax defaultSection)
        {
            if (defaultSection == null)
                return true;

            var statements = defaultSection.Statements;

            return statements.Any(IsStatementThrowsException);
        }

        private static bool IsStatementThrowsException(StatementSyntax statementSyntax)
        {
            if (statementSyntax is ThrowStatementSyntax)
                return true;

            if (statementSyntax is BlockSyntax blockSyntax)
                return blockSyntax.Statements.Any(IsStatementThrowsException);

            return false;
        }
    }
}

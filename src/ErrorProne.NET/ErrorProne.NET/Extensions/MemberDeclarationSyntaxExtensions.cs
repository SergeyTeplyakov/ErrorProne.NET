using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.Extensions
{
    public static class MemberDeclarationSyntaxExtensions
    {
        public static string GetMemberName(this MemberDeclarationSyntax member)
        {
            var method = member as MethodDeclarationSyntax;
            if (method != null) return method.Identifier.Text;

            var field = member as FieldDeclarationSyntax;
            if (field != null) return field.Declaration.Variables.First().Identifier.Text;

            var property = member as PropertyDeclarationSyntax;
            if (property != null) return property.Identifier.Text;

            return null;
        }
    }
}
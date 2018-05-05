using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.Core
{
    /// <nodoc />
    public static class SymbolExtensions
    {
        public static VariableDeclarationSyntax TryGetDeclarationSyntax(this IFieldSymbol symbol)
        {
            if (symbol.DeclaringSyntaxReferences.Length == 0)
            {
                return null;
            }

            var syntaxReference = symbol.DeclaringSyntaxReferences[0];
            return (VariableDeclarationSyntax) syntaxReference.GetSyntax().Parent;
        }

        public static PropertyDeclarationSyntax TryGetDeclarationSyntax(this IPropertySymbol symbol)
        {
            if (symbol.DeclaringSyntaxReferences.Length == 0)
            {
                return null;
            }

            var syntaxReference = symbol.DeclaringSyntaxReferences[0];
            return syntaxReference.GetSyntax().FirstAncestorOrSelf<PropertyDeclarationSyntax>();
        }

        public static MethodDeclarationSyntax TryGetDeclarationSyntax(this IMethodSymbol symbol)
        {
            if (symbol.DeclaringSyntaxReferences.Length == 0)
            {
                return null;
            }

            var syntaxReference = symbol.DeclaringSyntaxReferences[0];
            return syntaxReference.GetSyntax().FirstAncestorOrSelf<MethodDeclarationSyntax>();
        }
    }
}
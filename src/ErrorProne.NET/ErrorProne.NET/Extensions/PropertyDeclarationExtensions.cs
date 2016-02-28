using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.Extensions
{
    /// <summary>
    /// Set of extension method on <see cref="PropertyDeclarationSyntax"/> and <see cref="IPropertySymbol"/>.
    /// </summary>
    public static class PropertyDeclarationExtensions
    {
        public static bool HasInitializer(this PropertyDeclarationSyntax propertyDeclaration)
        {
            Contract.Requires(propertyDeclaration != null);
            return propertyDeclaration.DescendantNodes().Any(n => n is EqualsValueClauseSyntax);
        }

        public static AccessorDeclarationSyntax Getter(this PropertyDeclarationSyntax propertyDeclaration)
        {
            return
                propertyDeclaration
                .DescendantNodes()
                .OfType<AccessorDeclarationSyntax>()
                .FirstOrDefault(d => d.IsKind(SyntaxKind.GetAccessorDeclaration));
        }

        public static AccessorDeclarationSyntax Setter(this PropertyDeclarationSyntax propertyDeclaration)
        {
            return 
                propertyDeclaration
                .DescendantNodes()
                .OfType<AccessorDeclarationSyntax>()
                .FirstOrDefault(d => d.IsKind(SyntaxKind.SetAccessorDeclaration));
        }

        public static bool IsGetOnlyAutoProperty(this IPropertySymbol property, PropertyDeclarationSyntax propertyDeclaration)
        {
            Contract.Requires(property != null);
            Contract.Requires(propertyDeclaration != null);

            var getter = propertyDeclaration.Getter();
            if (getter == null) return false;

            return property.IsReadOnly && getter.SemicolonToken.IsKind(SyntaxKind.SemicolonToken);
        }
    }
}
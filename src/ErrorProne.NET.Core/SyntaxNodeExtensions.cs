using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.Extensions
{
    public static class SyntaxNodeExtensions
    {
        public static bool IsAutoProperty(this BasePropertyDeclarationSyntax syntax)
        {
            bool isAutoProperty = true;
            if (syntax.AccessorList != null)
            {
                foreach (var accessor in syntax.AccessorList.Accessors)
                {
                    if (accessor.Body != null || accessor.ExpressionBody != null)
                    {
                        isAutoProperty = false;
                    }
                }
            }
            else
            {
                isAutoProperty = false;
            }

            return isAutoProperty;
        }
        
        public static bool IsGetOnlyAutoProperty(this BasePropertyDeclarationSyntax property)
        {
            return property.IsAutoProperty() && !property.IsGetSetAutoProperty();
        }

        public static bool IsGetSetAutoProperty(this BasePropertyDeclarationSyntax property)
        {
            return property.AccessorList?.Accessors.Count == 2 && property.IsAutoProperty();
        }

        public static bool MarkedWithReadOnlyModifier(this BasePropertyDeclarationSyntax syntax)
        {
            return syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword));
        }
        
        public static bool MarkedWithReadOnlyModifier(this IPropertySymbol property)
        {
            return property
                .DeclaringSyntaxReferences
                .Select(p => (BasePropertyDeclarationSyntax)p.GetSyntax())
                .Any(p => MarkedWithReadOnlyModifier(p));
        }
    }
}
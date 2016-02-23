using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.Extensions
{
    public static class ExpressionExtensions
    {
        public static string GetLiteral(this ExpressionSyntax expression, SemanticModel semanticModel)
        {
            Contract.Requires(expression != null);
            Contract.Requires(semanticModel != null);

            if (expression is LiteralExpressionSyntax)
            {
                return expression.ToString();
            }

            IdentifierNameSyntax identifier = expression as IdentifierNameSyntax;
            if (identifier != null)
            {
                var referencedIdentifier = semanticModel.GetSymbolInfo(identifier);
                if (referencedIdentifier.Symbol != null)
                {
                    IFieldSymbol fieldReference = referencedIdentifier.Symbol as IFieldSymbol;
                    if (fieldReference == null)
                    {
                        return null;
                    }

                    // Checking if the field is a constant
                    string value = fieldReference.ConstantValue?.ToString();
                    if (value != null)
                    {
                        return value;
                    }

                    // Checking if the field is static readonly with literal initializer
                    if (fieldReference.IsStatic && fieldReference.IsReadOnly)
                    {
                        var referencedSyntax = fieldReference.DeclaringSyntaxReferences.FirstOrDefault();
                        VariableDeclaratorSyntax declarator = referencedSyntax?.GetSyntax() as VariableDeclaratorSyntax;
                        LiteralExpressionSyntax literal = declarator?.Initializer.Value as LiteralExpressionSyntax;
                        if (literal != null)
                        {
                            return literal.ToString();
                        }
                    }
                }
            }

            return null;
        }
    }
}
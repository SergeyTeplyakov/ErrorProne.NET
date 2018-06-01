using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.Core
{
    public static class MethodDeclarationSyntaxExtensions
    {
        [Pure]
        public static bool IsIteratorBlock(this MethodDeclarationSyntax method)
        {
            Contract.Requires(method != null);

            // very naive implementation
            return method.Body.DescendantNodes().Any(x => x is YieldStatementSyntax);
        }

        [Pure]
        public static ArgumentListSyntax AsArguments(this ParameterListSyntax parameters)
        {
            Contract.Requires(parameters != null);
            Contract.Ensures(Contract.Result<ArgumentListSyntax>() != null);

            return SyntaxFactory.ArgumentList(
                new SeparatedSyntaxList<ArgumentSyntax>().AddRange(
                    parameters.Parameters.Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Identifier)))));
        }

        [Pure]
        public static bool IsAsync(this MethodDeclarationSyntax method, SemanticModel semanticModel)
        {
            var methodSymbol = semanticModel.GetDeclaredSymbol(method);

            return methodSymbol?.IsAsync == true;
        }
    }
}
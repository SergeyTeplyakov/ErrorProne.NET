using System.Diagnostics.Contracts;
using System.Text.RegularExpressions;
using ErrorProne.NET.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Extensions {
    public static class SyntaxNodeAnalysisContextExtensions {
        public static INamedTypeSymbol GetClrType<T>(this SyntaxNodeAnalysisContext context) => context.SemanticModel.GetClrType(typeof(T));

        public static ITypeSymbol GetTypeSymbol(this SyntaxNodeAnalysisContext context, TypeSyntax typeSyntax) {
            Contract.Requires(typeSyntax != null);
            return context.SemanticModel.GetSymbolInfo(typeSyntax).Symbol as ITypeSymbol;
        }

        public static IMethodSymbol GetCtorSymbol(this SyntaxNodeAnalysisContext context, ObjectCreationExpressionSyntax objectCreationExpressionSyntax) {
            Contract.Requires(objectCreationExpressionSyntax != null);
            return context.SemanticModel.GetSymbolInfo(objectCreationExpressionSyntax).Symbol as IMethodSymbol;
        }
    }
}

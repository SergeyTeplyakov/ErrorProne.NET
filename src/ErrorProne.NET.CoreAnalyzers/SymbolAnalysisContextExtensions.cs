using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ErrorProne.NET.Core
{
    public static class SymbolAnalysisContextExtensions
    {
        public static bool TryGetSemanticModel(this SymbolAnalysisContext context, [NotNullWhen(true)]out SemanticModel? semanticModel)
        {
            if (context.Symbol.DeclaringSyntaxReferences.Length == 0)
            {
                semanticModel = null;
                return false;
            }

            semanticModel = context.Compilation.GetSemanticModel(context.Symbol.DeclaringSyntaxReferences[0].SyntaxTree);
            return true;
        }
    }
}
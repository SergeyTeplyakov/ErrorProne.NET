// --------------------------------------------------------------------
//  
// Copyright (c) Microsoft Corporation.  All rights reserved.
//  
// --------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Core
{
    public static class SymbolAnalysisContextExtensions
    {
        public static bool TryGetSemanticModel(this SymbolAnalysisContext context, out SemanticModel semanticModel)
        {
            if (context.Symbol.DeclaringSyntaxReferences.Length == 0)
            {
                semanticModel = null;
                return false;
            }

            semanticModel = context.Compilation.GetSemanticModel(context.Symbol.DeclaringSyntaxReferences[0].SyntaxTree);
            return semanticModel != null;
        }
    }
}
// --------------------------------------------------------------------
//  
// Copyright (c) Microsoft Corporation.  All rights reserved.
//  
// --------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.Core
{
    public static class FlowAnalysisExtensions
    {
        public static IEnumerable<DataFlowAnalysis?> AnalyzeDataflow(this SyntaxNode node, SemanticModel model)
        {
            if (node is PropertyDeclarationSyntax property && property.AccessorList?.Accessors.Count > 0)
            {
                foreach (var accessor in property.AccessorList.Accessors)
                {
                    if (accessor.Body != null)
                    {
                        yield return model.AnalyzeDataFlow(accessor.Body);
                    }
                    else if (accessor.ExpressionBody != null)
                    {
                        yield return model.AnalyzeDataFlow(accessor.ExpressionBody);
                    }
                }
            }
            else if (node is MethodDeclarationSyntax method)
            {
                yield return AnalyzeMethod(method, model);
            }
        }

        public static DataFlowAnalysis? AnalyzeMethod(this MethodDeclarationSyntax method, SemanticModel model)
        {
            if (method.Body != null)
            {
                return model.AnalyzeDataFlow(method.Body);
            }

            if (method.ExpressionBody != null)
            {
                return model.AnalyzeDataFlow(method.ExpressionBody.Expression);
            }

            return null;
        }

        public static IEnumerable<DataFlowAnalysis> AnalyzeMethod(this PropertyDeclarationSyntax property, SemanticModel model)
        {
            if (property.ExpressionBody != null)
            {
                yield return model.AnalyzeDataFlow(property.ExpressionBody);
            }
        }
    }
}
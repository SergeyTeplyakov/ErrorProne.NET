using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.ExceptionHandlingRules
{
    internal struct ExceptionReference
    {
        public ExceptionReference(ISymbol symbol, IdentifierNameSyntax identifier)
        {
            Contract.Requires(symbol != null);
            Contract.Requires(identifier != null);

            Symbol = symbol;
            Identifier = identifier;
        }

        public ISymbol Symbol { get; }
        public IdentifierNameSyntax Identifier { get; }
    }

    internal static class Helpers
    {
        public static List<ExceptionReference> GetExceptionIdentifierUsages(this SemanticModel semanticModel, SyntaxNode searchRoot)
        {
            var usages = (searchRoot ?? semanticModel.SyntaxTree.GetRoot())
                .DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(id => new { Symbol = semanticModel.GetSymbolInfo(id).Symbol, Id = id })
                .Where(x => x.Symbol != null && x.Symbol.ExceptionFromCatchBlock())
                .Select(x => new ExceptionReference(x.Symbol, x.Id))
                .ToList();

            return usages;
        }

        public static bool CatchIsTooGeneric(this CatchDeclarationSyntax declaration, SemanticModel semanticModel)
        {
            if (declaration == null)
            {
                return true;
            }

            var symbol = semanticModel.GetSymbolInfo(declaration.Type);
            if (symbol.Symbol == null)
            {
                return false;
            }

            var exception = semanticModel.Compilation.GetTypeByMetadataName(typeof(Exception).FullName);
            return symbol.Symbol.Equals(exception);
        }
    }
}
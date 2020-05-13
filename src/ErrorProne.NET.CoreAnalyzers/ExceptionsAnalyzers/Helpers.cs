using System.Collections.Generic;
using System.Diagnostics.ContractsLight;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.ExceptionAnalyzers
{
    internal readonly struct ExceptionReference
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
                .Select(x => new ExceptionReference(x.Symbol!, x.Id))
                .ToList();

            return usages;
        }

    }
}
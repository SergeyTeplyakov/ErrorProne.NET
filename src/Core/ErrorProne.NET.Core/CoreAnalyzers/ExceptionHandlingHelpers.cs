using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.Core.CoreAnalyzers
{
    public readonly struct ExceptionReference
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

    public static class ExceptionHandlingHelpers
    {
        public static List<ExceptionReference> GetExceptionIdentifierUsages(this SemanticModel semanticModel, SyntaxNode searchRoot)
        {
            var usages = (searchRoot ?? semanticModel.SyntaxTree.GetRoot())
                .DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(id => new {semanticModel.GetSymbolInfo(id).Symbol, Id = id })
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

            var symbol = semanticModel.GetSymbolInfo(declaration.Type).Symbol;
            if (symbol == null)
            {
                return false;
            }

            var compilation = semanticModel.Compilation;

            return IsGenericException(symbol, compilation);
        }

        private static bool IsGenericException(ISymbol symbol, Compilation compilation)
        {
            return symbol.IsClrType(compilation, typeof(Exception)) ||
                   symbol.IsClrType(compilation, typeof(AggregateException)) ||
                   symbol.IsClrType(compilation, typeof(TypeLoadException)) ||
                   symbol.IsClrType(compilation, typeof(TargetInvocationException));
        }
    }
}
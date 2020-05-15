using System;
using System.Collections.Generic;
using System.Diagnostics.ContractsLight;
using System.Linq;
using System.Reflection;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.CoreAnalyzers
{
    public readonly struct ExceptionReference : IEquatable<ExceptionReference>
    {
        public ExceptionReference(ISymbol symbol, IdentifierNameSyntax identifier)
        {
            Symbol = symbol;
            Identifier = identifier;
        }

        public ISymbol Symbol { get; }
        public IdentifierNameSyntax Identifier { get; }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is ExceptionReference other &&
                   Equals(other);
        }

        /// <inheritdoc/>
        public bool Equals(ExceptionReference other)
        {
            return Symbol.Equals(other.Symbol, SymbolEqualityComparer.Default) &&
                EqualityComparer<IdentifierNameSyntax>.Default.Equals(Identifier, other.Identifier);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return (Symbol, Identifier).GetHashCode();
        }

        /// <nodoc />
        public static bool operator ==(ExceptionReference left, ExceptionReference right)
        {
            return left.Equals(right);
        }

        /// <nodoc />
        public static bool operator !=(ExceptionReference left, ExceptionReference right)
        {
            return !(left == right);
        }
    }

    public static class ExceptionHandlingHelpers
    {
        public static List<ExceptionReference> GetExceptionIdentifierUsages(this SemanticModel semanticModel, SyntaxNode searchRoot)
        {
            var usages = (searchRoot ?? semanticModel.SyntaxTree.GetRoot())
                .DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(id => new
                {
                    Symbol = semanticModel.GetSymbolInfo(id).Symbol,
                    Id = id,
                })
                .Where(x => x.Symbol != null && x.Symbol.ExceptionFromCatchBlock())
                .Select(x => new ExceptionReference(x.Symbol!, x.Id))
                .ToList();

            return usages;
        }

        public static bool CatchIsTooGeneric(this CatchDeclarationSyntax declaration, SemanticModel semanticModel)
        {
            if (declaration == null)
            {
                return true;
            }

            if (declaration.Parent is CatchClauseSyntax catchSyntax && catchSyntax.Filter != null)
            {
                // catch block is not too generic when a filter is specified.
                return false;
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
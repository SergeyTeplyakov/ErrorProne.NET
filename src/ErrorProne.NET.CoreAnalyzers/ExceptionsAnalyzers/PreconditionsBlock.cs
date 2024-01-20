using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.ContractsLight;
using System.Linq;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.ExceptionsAnalyzers
{
    internal sealed class IfThrowPrecondition
    {
        public IfThrowPrecondition(StatementSyntax ifThrowStatement, ThrowStatementSyntax throwStatement)
        {
            Contract.Requires(ifThrowStatement != null);
            Contract.Requires(throwStatement != null);

            IfThrowStatement = ifThrowStatement;
            ThrowStatement = throwStatement;
        }

        public StatementSyntax IfThrowStatement { get; }
        public ThrowStatementSyntax ThrowStatement { get; }
    }
}
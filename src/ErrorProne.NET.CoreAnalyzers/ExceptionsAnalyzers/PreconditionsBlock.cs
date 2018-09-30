using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.ContractsLight;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.ExceptionsAnalyzers
{
    internal sealed class IfThrowPrecondition
    {
        public IfThrowPrecondition(StatementSyntax ifThrowStaement, ThrowStatementSyntax throwStatement)
        {
            Contract.Requires(ifThrowStaement != null);
            Contract.Requires(throwStatement != null);

            IfThrowStaement = ifThrowStaement;
            ThrowStatement = throwStatement;
        }

        public StatementSyntax IfThrowStaement { get; }
        public ThrowStatementSyntax ThrowStatement { get; }
    }

    /// <summary>
    /// Class that holds all checks that could be considered as a method preconditions.
    /// </summary>
    internal sealed class PreconditionsBlock
    {
        public PreconditionsBlock(List<IfThrowPrecondition> preconditions)
        {
            Preconditions = preconditions.ToImmutableList();
        }

        public ImmutableList<IfThrowPrecondition> Preconditions { get; }

        public static PreconditionsBlock GetPreconditions(MethodDeclarationSyntax method, SemanticModel semanticModel)
        {
            Contract.Requires(method != null);

            var preconditions = new List<IfThrowPrecondition>();

            // Precondition block ends when something exception precondition check is met.
            foreach (var statement in method.Body.Statements)
            {
                // Currently, If-throw precondition means that
                // if statement has only one statement in the if block
                // and this statement is a throw of type ArgumentException
                var ifThrowStatement = statement as IfStatementSyntax;
                if (ifThrowStatement == null) break;

                var block = ifThrowStatement.Statement as BlockSyntax;
                if (block != null && block.Statements.Count != 1) break;

                var throwStatementCandidate = block != null ? block.Statements[0] : ifThrowStatement.Statement;

                // The only valid case (when the processing should keep going)
                // is when the if block has one statement and that statment is a throw of ArgumentException
                if (IsThrowArgumentExceptionStatement(throwStatementCandidate, semanticModel))
                {
                    preconditions.Add(new IfThrowPrecondition(statement, (ThrowStatementSyntax) throwStatementCandidate));
                }
                else
                {
                    break;
                }
            }

            return new PreconditionsBlock(preconditions);
        }

        private static bool IsThrowArgumentExceptionStatement(StatementSyntax statement, SemanticModel semanticModel)
        {
            var throwStatement = statement as ThrowStatementSyntax;

            var objectCreation = throwStatement?.Expression as ObjectCreationExpressionSyntax;
            if (objectCreation == null) return false;

            var symbol = semanticModel.GetSymbolInfo(objectCreation.Type).Symbol;
            return symbol.IsArgumentExceptionType(semanticModel);
        }
    }
}
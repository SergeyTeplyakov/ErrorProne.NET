using System.Linq;
using System.Threading;
using ErrorProne.NET.Core;
using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.AsyncAnalyzers
{
    /// <summary>
    /// This analyzer warns when a result of a call to <see cref="CancellationToken.Register(System.Action)"/> is not observed for a non-local <see cref="CancellationTokenSource"/>.
    /// </summary>
    /// <remarks>
    /// FxCop analyzers can detect that a disposable instance is not disposed correctly, but not everyone uses them and currently (as of 2/22/2021) they don't report any issues
    /// when the result of <code>token.Register()</code> is not disposed.
    /// If the <see cref="CancellationTokenRegistration"/> is obtained from a long-lived token (non-local), and the instance is not disposed then an application may have a memory leak.
    /// (a callback may capture locals that will live as long as the token).
    /// This is implementation is not as fancy as the one in DotNet FxCop analyzers that uses cross-procedural dataflow analysis.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CancellationTokenRegistrationAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.CancellationTokenRegistrationIssue;

        private const string Title = "Observe and Dispose a 'CancellationTokenRegistration' to avoid memory leaks.";

        private const string Description = "Failure to dispose 'CancellationTokenRegistration' may cause a memory leak if obtained from a non-local token.";
        private const string Category = "Performance";

        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        public static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, Title, Category, Severity, isEnabledByDefault: true, description: Description);

        /// <nodoc />
        public CancellationTokenRegistrationAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterOperationAction(AnalyzeExpressionStatement, OperationKind.ExpressionStatement);
        }

        private void AnalyzeExpressionStatement(OperationAnalysisContext context)
        {
            if (context.Operation is IExpressionStatementOperation expressionStatement
                && expressionStatement.Operation is IInvocationOperation invocation) 
            {
                if (invocation.Type.IsClrType(context.Compilation, typeof(CancellationTokenRegistration)))
                {
                    var semanticModel = context.Compilation.GetSemanticModel(context.Operation.Syntax.SyntaxTree);

                    var target = invocation.Instance;
                    // Covering the cases like 'cts.Token.Register'.
                    if (invocation.Instance is IPropertyReferenceOperation topLevelPropertyReference)
                    {
                        target = topLevelPropertyReference.Instance;
                    }
                    
                    // The diagnostics should be emitted only in some cases:
                    // When a token is an argument, field, property, obtained from a method call
                    // (not a local variable and not obtained from a local CancellationTokenSource).
                    if (target is not ILocalReferenceOperation)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Syntax.GetLocation()));
                    }
                    else if (target is ILocalReferenceOperation localReference)
                    {
                        // Vary naive approach to understand where a local reference points to.
                        var localDeclaringOperation = GetLocalReferenceDeclaringOperation(localReference, semanticModel, context.CancellationToken);
                        if (localDeclaringOperation is IVariableDeclaratorOperation variableDeclarator)
                        {
                            // Initializer here is something like: var token = cts.Token.
                            var initializer = variableDeclarator.Initializer?.Value;

                            if (initializer is IPropertyReferenceOperation propertyReference)
                            {
                                // This is the most common case: getting a token from a cancellation token source.
                                if (propertyReference.Instance is not ILocalReferenceOperation)
                                {
                                    // A token is obtained from a field or a property.
                                    context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Syntax.GetLocation()));
                                }
                            }
                        }
                    }
                }
            }
        }
        
        private static IOperation? GetLocalReferenceDeclaringOperation(ILocalReferenceOperation localReference, SemanticModel semanticModel, CancellationToken token)
        {
            var localSymbolDeclaringSyntax = localReference.Local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(token);
            if (localSymbolDeclaringSyntax != null)
            {
                return semanticModel.GetOperation(localSymbolDeclaringSyntax);
            }

            return null;
        }
    }
}
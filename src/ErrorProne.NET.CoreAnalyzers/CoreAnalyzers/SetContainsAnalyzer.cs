using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// An analyzer that warns about <see cref="Enumerable.Contains{TSource}(IEnumerable{TSource},TSource, IEqualityComparer{TSource})"/> usage with sets.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class SetContainsAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public static DiagnosticDescriptor Rule => DiagnosticDescriptors.EPC23;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <nodoc />
        public SetContainsAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterOperationAction(AnalyzeInvocationOperation, OperationKind.Invocation);
        }

        private void AnalyzeInvocationOperation(OperationAnalysisContext context)
        {
            // Looking for 'Enumerable.Contains' call that passes an extra argument of type 'StringComparer'.
            var invocationOperation = (IInvocationOperation)context.Operation;

            var receiverType = invocationOperation.GetReceiverType(includeLocal: true);

            if (
                IsEnumerableContains(invocationOperation.TargetMethod, context.Compilation) &&
                IsSet(receiverType, context.Compilation))
            {
                var diagnostic = Diagnostic.Create(Rule, invocationOperation.Syntax.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }

            static bool IsEnumerableContains(IMethodSymbol methodSymbol, Compilation compilation)
            {
                return methodSymbol.Name == nameof(Enumerable.Contains) &&
                       // There are two overloads, one that just takes the value
                       // and the second one that takes StringComparer,
                       // and only the second one is linear.
                       methodSymbol.Parameters.Length == 3 &&
                       methodSymbol.ContainingType.IsClrType(compilation, typeof(System.Linq.Enumerable));
            }

            static bool IsSet(ITypeSymbol? typeSymbol, Compilation compilation)
            {
                if (typeSymbol is not INamedTypeSymbol { IsGenericType: true } nt)
                {
                    return false;
                }

                return typeSymbol.IsGenericType(compilation, typeof(HashSet<>)) ||
                       typeSymbol.IsGenericType(compilation, typeof(ISet<>)) ||
                       nt.ConstructedFrom
                           .AllInterfaces
                           .Any(i => i.IsGenericType(compilation, typeof(ISet<>)));
            }
        }
    }
}
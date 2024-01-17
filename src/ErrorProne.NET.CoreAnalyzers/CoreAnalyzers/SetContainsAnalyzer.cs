using System;
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
    /// An analyzer that warns about incorrect usage of concurrent collection types.
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
            try
            {
                // Detecting the calls to concurrentDictionaryInstance.OrderBy because
                // it may fail with ArgumentException if the collection is being mutated concurrently at the same time.
                var invocationOperation = (IInvocationOperation)context.Operation;

                var receiverType = invocationOperation.GetReceiverType(includeLocal: true);
                
                if (
                    IsEnumerableContains(invocationOperation.TargetMethod, context.Compilation) &&
                    IsSet(receiverType, context.Compilation))
                {
                    var diagnostic = Diagnostic.Create(Rule, invocationOperation.Syntax.GetLocation());
                    context.ReportDiagnostic(diagnostic);
                }
            }
            catch (Exception)
            {
                throw;
                //Debugger.Launch();
                //throw new Exception(e.StackTrace);
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
                       (typeSymbol as INamedTypeSymbol)
                           ?.ConstructedFrom
                           .AllInterfaces
                           .Any(i => i.IsGenericType(compilation, typeof(ISet<>))) == true;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.AsyncAnalyzers
{
    /// <summary>
    /// An analyzer that warns about incorrect usage of concurrent collection types.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ConcurrentCollectionAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.UsageIsNotThreadSafe;

        private static readonly string Title = "The API is not thread-safe.{0}";

        private static readonly string Description = "The API is not thread safe and can cause runtime failures.";
        private const string Category = "Concurrency";

        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        public static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, Title, Category, Severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <nodoc />
        public ConcurrentCollectionAnalyzer()
            : base(supportFading: true, Rule)
        {
        }

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            context.RegisterOperationAction(AnalyzeInvocationOperation, OperationKind.Invocation);
        }

        private static readonly HashSet<string> ConcurrentDictionaryUnsafeLinqOperations = new HashSet<string>()
        {
            nameof(Enumerable.OrderBy),
            nameof(Enumerable.OrderByDescending),
            nameof(Enumerable.ToList),
            nameof(Enumerable.ToArray),
            nameof(Enumerable.Reverse), // Reverse uses Buffer under the hood and is not thread safe
        };

        private void AnalyzeInvocationOperation(OperationAnalysisContext context)
        {
            try
            {
                // Detecting the calls to concurrentDictionaryInstance.OrderBy because
                // it may fail with ArgumentException if the collection is being mutated concurrently at the same time.
                var invocationOperation = (IInvocationOperation)context.Operation;

                var receiverType = GetReceiverType(context.Compilation, invocationOperation);

                var targetMethodName = invocationOperation.TargetMethod.Name;
                if (receiverType != null &&
                    invocationOperation.TargetMethod.ContainingType.ToDisplayString() == "System.Linq.Enumerable" &&
                    ConcurrentDictionaryUnsafeLinqOperations.Contains(targetMethodName))
                {
                    if (receiverType.ToDisplayString()
                            .StartsWith("System.Collections.Concurrent.ConcurrentDictionary<") == true)
                    {
                        string extra =
                            $" Calling '{invocationOperation.TargetMethod.Name}' on a shared ConcurrentDictionary instance is not thread-safe and may fail with ArgumentException.";

                        // Extra hint in case ToList or Enumerable.ToArray(cd) is used: the only thread-safe alternative is to use an instance ToArray method.
                        if (targetMethodName == "ToList" || targetMethodName == "ToArray")
                        {
                            extra += " Use instance ToArray() method instead.";
                        }

                        var diagnostic = Diagnostic.Create(Rule, invocationOperation.Syntax.GetLocation(), extra);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
            catch (Exception e)
            {
                //Debugger.Launch();
                throw new Exception(e.StackTrace);
            }
        }

        private ITypeSymbol? GetReceiverType(Compilation compilation, IInvocationOperation invocationOperation)
        {
            // We have (at least) two cases here:
            // instance.ToList() and
            // Enumerable.ToList(instance).
            if (invocationOperation.Arguments.Length == 0)
            {
                return null;
            }

            var firstArg = invocationOperation.Arguments[0];
            var semanticModel = compilation.GetSemanticModel(firstArg.Syntax.SyntaxTree);
            var argumentOperation = semanticModel.GetOperation(firstArg.Syntax);

            if (argumentOperation is IArgumentOperation)
            {
                // This is the same argument operation that we obtained before.
                // It means that this is a real argument like Enumerable.ToList(arg)
                // and not something like arg.ToList();

                if (!(firstArg.Syntax.ChildNodes().FirstOrDefault() is IdentifierNameSyntax argumentIdentifier))
                {
                    // TODO: is it actually possible?
                    return null;
                }

                return semanticModel.GetTypeInfo(argumentIdentifier).Type;
            }

            // This is 'foo.ToList()' case.
            // Just getting a type of an operation but we need to exclude locals.

            if (argumentOperation is ILocalReferenceOperation)
            {
                // Important NOTE: excluding local variables, because it is way likely that the local is used in a shared context.
                // In most cases the locals are used in some kind of fork-join scenario, when the local is created,
                // populated in parallel via Parallel.For or something similar and then processed after that.
                // It is still possible that this code may cause issues, but because this pattern is relatively popular
                // its better to avoid false positives here.
                return null;
            }

            return argumentOperation?.Type;
        }
    }
}
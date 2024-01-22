using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Core;
using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;
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
        public static DiagnosticDescriptor Rule => DiagnosticDescriptors.ERP031;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <nodoc />
        public ConcurrentCollectionAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
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

                var receiverType = invocationOperation.GetReceiverType();

                var targetMethodName = invocationOperation.TargetMethod.Name;
                if (receiverType != null &&
                    invocationOperation.TargetMethod.ContainingType.ToDisplayString() == "System.Linq.Enumerable" &&
                    ConcurrentDictionaryUnsafeLinqOperations.Contains(targetMethodName))
                {
                    if (receiverType.ToDisplayString().StartsWith("System.Collections.Concurrent.ConcurrentDictionary<") == true)
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
    }
}
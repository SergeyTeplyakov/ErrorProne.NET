using System.Collections.Immutable;
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

        private void AnalyzeInvocationOperation(OperationAnalysisContext context)
        {
            // Detecting the calls to concurrentDictionaryInstance.OrderBy because
            // it may fail with ArgumentException if the collection is being mutated concurrently at the same time.
            var invocationOperation = (IInvocationOperation)context.Operation;

            var firstArg = invocationOperation.Arguments[0];
            var semanticModel = context.Compilation.GetSemanticModel(firstArg.Syntax.SyntaxTree);

            // For concurrentDictionaryInstance.OrderBy(x => x.Key) case the first argument's syntax is an identifier.
            var argumentIdentifier = firstArg.Syntax as IdentifierNameSyntax;
            if (argumentIdentifier is null)
            {
                // But in Enumerable.OrderBy(concurrentDictionaryInstance, x => x.Key) case the argument
                // is of type ArgumentSyntax and we have to get the first child in order to get the indentifier.
                argumentIdentifier = firstArg.Syntax.ChildNodes().FirstOrDefault() as IdentifierNameSyntax;
            }

            if (argumentIdentifier == null)
            {
                // TODO: is it actually possible?
                return;
            }

            var typeInfo = semanticModel.GetTypeInfo(argumentIdentifier);
            
            if (typeInfo.Type != null &&
                invocationOperation.TargetMethod.Name == "OrderBy" &&
                invocationOperation.TargetMethod.ContainingType.ToDisplayString() == "System.Linq.Enumerable")
            {
                if (typeInfo.Type?.ToDisplayString().StartsWith("System.Collections.Concurrent.ConcurrentDictionary<") == true)
                {
                    string extra = " Calling OrderBy on ConcurrentDictionary is not thread-safe and may fail with ArgumentException.";
                    var diagnostic = Diagnostic.Create(Rule, invocationOperation.Syntax.GetLocation(), extra);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
using System.Collections.Immutable;
using System.Diagnostics.ContractsLight;
using System.Threading.Tasks;
using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace ErrorProne.NET.AsyncAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RedundantConfigureAwaitFalseAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public static string DiagnosticId => Rule.Id;

        /// <nodoc />
        public static DiagnosticDescriptor Rule => DiagnosticDescriptors.EPC14;

        /// <nodoc />
        public RedundantConfigureAwaitFalseAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(context =>
            {
                var compilation = context.Compilation;

                var configureAwaitConfig = ConfigureAwaitConfiguration.TryGetConfigureAwait(context.Compilation);
                if (configureAwaitConfig != ConfigureAwait.DoNotUseConfigureAwait)
                {
                    return;
                }

                var taskType = compilation.GetTypeByMetadataName(typeof(Task).FullName);
                if (taskType is null)
                {
                    return;
                }

                var configureAwaitMethods = taskType.GetMembers("ConfigureAwait").OfType<IMethodSymbol>().ToImmutableArray();
                if (configureAwaitMethods.IsEmpty)
                {
                    return;
                }

                context.RegisterOperationAction(context => AnalyzeAwaitOperation(context, configureAwaitMethods), OperationKind.Await);
            });
            
        }

        private static void AnalyzeAwaitOperation(OperationAnalysisContext context, ImmutableArray<IMethodSymbol> configureAwaitMethods)
        {
            var awaitOperation = (IAwaitOperation)context.Operation;

            if (awaitOperation.Operation is IInvocationOperation configureAwaitOperation &&
                configureAwaitMethods.Contains(configureAwaitOperation.TargetMethod))
            {
                if (configureAwaitOperation.Arguments.Length != 0 &&
                    configureAwaitOperation.Arguments[0].Value is ILiteralOperation literal &&
                    literal.ConstantValue.Value?.Equals(false) == true)
                {
                    var location = configureAwaitOperation.Syntax.GetLocation();

                    // Need to find 'ConfigureAwait' node.
                    if (configureAwaitOperation.Syntax is InvocationExpressionSyntax i &&
                        i.Expression is
                        MemberAccessExpressionSyntax mae)
                    {
                        // This is a really weird way for getting a location for 'ConfigureAwait(false)' span!

                        var argsLocation = i.ArgumentList.GetLocation();
                        var nameLocation = mae.Name.GetLocation().SourceSpan;

                        Contract.Assert(argsLocation.SourceTree != null);
                        location = Location.Create(argsLocation.SourceTree,
                            TextSpan.FromBounds(nameLocation.Start, argsLocation.SourceSpan.End));
                    }

                    var diagnostic = Diagnostic.Create(Rule, location);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
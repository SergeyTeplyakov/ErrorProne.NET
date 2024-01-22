using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.AsyncAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ConfigureAwaitRequiredAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public static string DiagnosticId => Rule.Id;

        /// <nodoc />
        public static DiagnosticDescriptor Rule => DiagnosticDescriptors.EPC15;

        /// <nodoc />
        public ConfigureAwaitRequiredAnalyzer()
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
                if (configureAwaitConfig != ConfigureAwait.UseConfigureAwaitFalse)
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

                var yieldAwaitable = compilation.GetTypeByMetadataName(typeof(YieldAwaitable).FullName);

                context.RegisterOperationAction(context => AnalyzeAwaitOperation(context, configureAwaitMethods, yieldAwaitable), OperationKind.Await);
            });
            
        }

        private static void AnalyzeAwaitOperation(OperationAnalysisContext context, ImmutableArray<IMethodSymbol> configureAwaitMethods, INamedTypeSymbol? yieldAwaitable)
        {
            var awaitOperation = (IAwaitOperation)context.Operation;

            if (awaitOperation.Operation is IInvocationOperation configureAwaitOperation)
            {
                if (configureAwaitMethods.Contains(configureAwaitOperation.TargetMethod))
                {
                    return;
                }

                if (SymbolEqualityComparer.Default.Equals(configureAwaitOperation.Type, yieldAwaitable))
                {
                    return;
                }
            }

            var location = awaitOperation.Syntax.GetLocation();

            var diagnostic = Diagnostic.Create(Rule, location);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
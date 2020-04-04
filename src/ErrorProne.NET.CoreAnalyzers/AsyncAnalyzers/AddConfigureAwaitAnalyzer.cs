using System.Runtime.CompilerServices;
using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using CompilationExtensions = ErrorProne.NET.Core.CompilationExtensions;

namespace ErrorProne.NET.AsyncAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AddConfigureAwaitAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.ConfigureAwaitFalseMustBeUsed;

        private static readonly string Title = "ConfigureAwait(false) must be used.";

        private static readonly string Description = "The assembly is configured to use .ConfigureAwait(false)";
        private const string Category = "CodeSmell";

        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        public static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, Title, Category, Severity, isEnabledByDefault: true, description: Description);

        /// <nodoc />
        public AddConfigureAwaitAnalyzer()
            : base(supportFading: true, Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeAwaitExpression, SyntaxKind.AwaitExpression);
        }

        private void AnalyzeAwaitExpression(SyntaxNodeAnalysisContext context)
        {
            var invocation = (AwaitExpressionSyntax)context.Node;

            var configureAwaitConfig = ConfigureAwaitConfiguration.TryGetConfigureAwait(context.Compilation);
            if (configureAwaitConfig == ConfigureAwait.UseConfigureAwaitFalse)
            {
                var operation = context.SemanticModel.GetOperation(invocation, context.CancellationToken);
                if (operation is IAwaitOperation awaitOperation)
                {
                    if (awaitOperation.Operation is IInvocationOperation configureAwaitOperation)
                    {
                        if (configureAwaitOperation.TargetMethod.IsConfigureAwait(context.Compilation))
                        {
                            return;
                        }

                        if (CompilationExtensions.IsClrType(configureAwaitOperation.Type, context.Compilation, typeof(YieldAwaitable)))
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
    }
}
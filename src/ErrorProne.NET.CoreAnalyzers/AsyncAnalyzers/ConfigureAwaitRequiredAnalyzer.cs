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
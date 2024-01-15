using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;

namespace ErrorProne.NET.StructAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ExplicitInParameterAnalyzer : DiagnosticAnalyzer
    {
        /// <nodoc />
        public static string DiagnosticId => Rule.Id;
        /// <nodoc />
        public static DiagnosticDescriptor Rule => DiagnosticDescriptors.EPS09;

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterOperationAction(AnalyzerInvocation, OperationKind.Invocation);
        }

        private static void AnalyzerInvocation(OperationAnalysisContext context)
        {
            var invocation = (IInvocationOperation)context.Operation;
            foreach (var argument in invocation.Arguments)
            {
                if (argument.Parameter != null && argument.Parameter.RefKind != RefKind.In)
                {
                    continue;
                }

                if (argument.IsImplicit)
                {
                    continue;
                }

                if (argument.Syntax is ArgumentSyntax argumentSyntax && argumentSyntax.RefKindKeyword.IsKind(SyntaxKind.InKeyword))
                {
                    continue;
                }

                if (argument.Value?.ConstantValue.HasValue ?? false)
                {
                    continue;
                }

                if (argument.Value is IObjectCreationOperation)
                {
                    continue;
                }

                if (argument.Value is IUnaryOperation)
                {
                    // Skipping cases like '-value'
                    continue;
                }

                if (argument.Value is IInstanceReferenceOperation instanceReference
                    && instanceReference.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance
                    && argument.Value.Type?.IsReferenceType == true)
                {
                    continue;
                }

                if (argument.Value is IPropertyReferenceOperation propertyReference
                    && propertyReference.Property.RefKind == RefKind.None)
                {
                    continue;
                }

                if (argument.Value is IInvocationOperation methodReference
                    && methodReference.TargetMethod.RefKind == RefKind.None)
                {
                    continue;
                }

                if (argument.Value is IConversionOperation conversion
                    && conversion.Operand is IDefaultValueOperation)
                {
                    continue;
                }

                if (argument.Value is IDefaultValueOperation)
                {
                    continue;
                }

                if (argument.Value is ITupleOperation)
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(Rule, argument.Syntax.GetLocation(), argument.Parameter?.Name));
            }
        }
    }
}

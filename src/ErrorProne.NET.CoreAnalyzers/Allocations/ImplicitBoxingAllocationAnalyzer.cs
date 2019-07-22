using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.ContractsLight;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

#nullable enable

namespace ErrorProne.NET.CoreAnalyzers.Allocations
{
    /// <summary>
    /// Analyzer that warns when the result of a method invocation is ignore (when it potentially, shouldn't).
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ImplicitBoxingAllocationAnalyzer : DiagnosticAnalyzer
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.ImplicitBoxing;

        private static readonly string Title = "Boxing allocation.";
        private static readonly string Message = "Boxing allocation of type '{0}' because of invocation of member '{1}'.";

        private static readonly string Description = "Return values of some methods should be observed.";
        private const string Category = "CodeSmell";

        // Using warning for visibility purposes
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        public static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, Message, Category, Severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <nodoc />
        public ImplicitBoxingAllocationAnalyzer() 
            //: base(supportFading: false, diagnostics: Rule)
        {
        }

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeInvocationExpression, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            var targetSymbol = context.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol;

            if (targetSymbol != null && invocation.Expression is MemberAccessExpressionSyntax ms)
            {
                var sourceOperation = context.SemanticModel.GetOperation(ms.Expression);

                if (sourceOperation != null)
                {
                    if (sourceOperation.Type?.IsValueType == true && targetSymbol.ContainingType?.IsValueType == false)
                    {
                        // The source expression is a struct, but the target method ends in System.Object, System.ValueType or System.Enum
                        var fullTargetMemberName = targetSymbol.ToDisplayString();
                        context.ReportDiagnostic(Diagnostic.Create(Rule, ms.Name.GetLocation(), sourceOperation.Type.Name, fullTargetMemberName));
                    }
                }
            }
        }
    }
}
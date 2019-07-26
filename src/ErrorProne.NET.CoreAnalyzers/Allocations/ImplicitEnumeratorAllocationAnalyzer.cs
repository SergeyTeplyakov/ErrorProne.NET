using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.ContractsLight;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using ErrorProne.NET.AsyncAnalyzers;
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
    public sealed class ImplicitEnumeratorAllocationAnalyzer : DiagnosticAnalyzer
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.ImplicitEnumeratorBoxing;

        private static readonly string Title = "Boxing enumerator.";
        private static readonly string Message = "Allocating or boxing enumerator of type {0}";

        private static readonly string Description = "Return values of some methods should be observed.";
        private const string Category = "CodeSmell";

        // Using warning for visibility purposes
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        public static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, Message, Category, Severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <nodoc />
        public ImplicitEnumeratorAllocationAnalyzer() 
            //: base(supportFading: false, diagnostics: Rule)
        {
        }

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeForeachStatement, SyntaxKind.ForEachStatement);
        }

        private void AnalyzeForeachStatement(SyntaxNodeAnalysisContext context)
        {
            if (NoHiddenAllocationsConfiguration.ShouldNotDetectAllocationsFor(context.Node, context.SemanticModel))
            {
                return;
            }

            var foreachStatement = (ForEachStatementSyntax) context.Node;

            if (context.SemanticModel.GetOperation(foreachStatement) is IForEachLoopOperation foreachOperation)
            {
                if (foreachOperation.Collection.Type.SpecialType == SpecialType.System_String)
                {
                    return;
                }

                if (foreachOperation.Collection is IConversionOperation co && co.Operand?.Type is IArrayTypeSymbol)
                {
                    // this is foreach over an array that is converted to for loop.
                    return;
                }

                var getEnumeratorMethod = foreachOperation.GetEnumeratorMethod();
                if (getEnumeratorMethod?.ReturnType.IsValueType == false)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, foreachStatement.Expression.GetLocation(), getEnumeratorMethod.ReturnType.Name));
                }
            }
        }
    }
}
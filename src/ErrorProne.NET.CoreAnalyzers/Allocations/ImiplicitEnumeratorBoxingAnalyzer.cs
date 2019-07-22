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
    public sealed class ImiplicitEnumeratorBoxingAnalyzer : DiagnosticAnalyzer
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.ImplicitBoxing;

        private static readonly string Title = "Boxing enumerator.";
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
        public ImiplicitEnumeratorBoxingAnalyzer() 
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
            var foreachStatment = (ForEachStatementSyntax) context.Node;

            if (context.SemanticModel.GetOperation(foreachStatment) is IForEachLoopOperation foreachOperation)
            {
                var infoProperty = foreachOperation.GetType().GetTypeInfo().BaseType.GetTypeInfo().GetDeclaredProperty("Info");

                Contract.Assert(infoProperty != null);

                var infoObject = infoProperty.GetMethod.Invoke(foreachOperation, new object[0]);

                if (infoObject != null)
                {
                    var getEnumeratorMethodField = infoObject.GetType().GetTypeInfo().GetDeclaredField("GetEnumeratorMethod");

                    Contract.Assert(getEnumeratorMethodField != null);

                    var getEnumeratorMethod = (IMethodSymbol)getEnumeratorMethodField.GetValue(infoObject);

                    if (getEnumeratorMethod?.ReturnType.IsValueType == false)
                    {
                        context.ReportDiagnostic(Rule, "Call to enumerator");
                    }
                }

            }



        }

        //private GetEnumeratorIsStruct(IForEachLoopOperation foreachOperation)
        //{
        //    foreachOperation.GetType().GetTypeInfo().GetDeclaredProperty();
        //}
    }
}
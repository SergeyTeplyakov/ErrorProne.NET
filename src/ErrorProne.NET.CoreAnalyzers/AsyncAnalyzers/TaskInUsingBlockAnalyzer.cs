using System;
using System.Linq;
using ErrorProne.NET.Core;
using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.AsyncAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class TaskInUsingBlockAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public static DiagnosticDescriptor Rule => DiagnosticDescriptors.EPC26;

        /// <nodoc />
        public TaskInUsingBlockAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterOperationAction(AnalyzeUsing, OperationKind.Using);
            context.RegisterOperationAction(AnalyzeUsingDeclaration, OperationKind.UsingDeclaration);
        }

        private void AnalyzeUsingDeclaration(OperationAnalysisContext context)
        {
            IUsingDeclarationOperation usingDeclarationOperation = (IUsingDeclarationOperation)context.Operation;
            AnalyzeCore(context,usingDeclarationOperation);
        }

        private void AnalyzeUsing(OperationAnalysisContext context)
        {
            IUsingOperation usingOperation = (IUsingOperation)context.Operation;

            AnalyzeCore(context, usingOperation);
        }

        private void AnalyzeCore(OperationAnalysisContext context, IOperation parentOperation)
        {
            foreach (var operation in parentOperation.EnumerateChildOperations())
            {
                if (operation is IVariableInitializerOperation variableInitializer)
                {
                    if (variableInitializer.Value.Type.IsTaskLike(context.Compilation, TaskLikeTypes.TasksOnly))
                    {
                        var diagnostic = Diagnostic.Create(Rule, parentOperation.Syntax.GetLocation());
                        context.ReportDiagnostic(diagnostic);
                        break;
                    }
                }
                else
                {
                    if (operation.Type.IsTaskLike(context.Compilation, TaskLikeTypes.TasksOnly))
                    {
                        var diagnostic = Diagnostic.Create(Rule, parentOperation.Syntax.GetLocation());
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
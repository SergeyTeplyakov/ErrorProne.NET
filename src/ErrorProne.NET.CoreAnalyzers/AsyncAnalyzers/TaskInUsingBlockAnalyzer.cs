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

        private static bool HasTaskLikeVariableDeclaration(IOperation operation, Compilation compilation)
        {
            return operation.EnumerateChildOperations().OfType<IVariableInitializerOperation>()
                .Any(t => t.Value.Type.IsTaskLike(compilation));
        }

        private void AnalyzeUsingDeclaration(OperationAnalysisContext context)
        {
            IUsingDeclarationOperation usingDeclarationOperation = (IUsingDeclarationOperation)context.Operation;
            if (HasTaskLikeVariableDeclaration(usingDeclarationOperation.DeclarationGroup, context.Compilation))
            {
                var diagnostic = Diagnostic.Create(Rule, usingDeclarationOperation.Syntax.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
            //AnalyzeCore(context,usingDeclarationOperation);
        }

        private void AnalyzeUsing(OperationAnalysisContext context)
        {
            IUsingOperation usingOperation = (IUsingOperation)context.Operation;
            if (
                // This is needed to cover the case like 'using(GetTask()) {}'
                usingOperation.Resources.Type.IsTaskLike(context.Compilation) ||
                HasTaskLikeVariableDeclaration(usingOperation.Resources, context.Compilation))
                // usingOperation.Locals.Any(l => l.Type.IsTaskLike(context.Compilation)) ||
                
                //(usingOperation.Body as IBlockOperation)?.Locals.Any(l => l.Type.IsTaskLike(context.Compilation)) == true)
            {
                var diagnostic = Diagnostic.Create(Rule, usingOperation.Syntax.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
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
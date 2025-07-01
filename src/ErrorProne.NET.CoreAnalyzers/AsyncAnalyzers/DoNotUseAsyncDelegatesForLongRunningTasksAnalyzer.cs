using System.Threading.Tasks;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using ErrorProne.NET.CoreAnalyzers;

namespace ErrorProne.NET.AsyncAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DoNotUseAsyncDelegatesForLongRunningTasksAnalyzer : DiagnosticAnalyzerBase
    {
        public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptors.EPC36;

        /// <nodoc />
        public DoNotUseAsyncDelegatesForLongRunningTasksAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        }

        private void AnalyzeInvocation(OperationAnalysisContext context)
        {
            var invocation = (IInvocationOperation)context.Operation;

            // Check if this is Task.Factory.StartNew
            if (!IsTaskFactoryStartNew(invocation, context.Compilation))
            {
                return;
            }

            // Check if TaskCreationOptions.LongRunning is used
            if (!HasLongRunningOption(invocation))
            {
                return;
            }

            // Check if the first argument is an async delegate
            if (invocation.Arguments.Length > 0 && IsAsyncDelegate(invocation.Arguments[0].Value))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Syntax.GetLocation()));
            }
        }

        private static bool IsTaskFactoryStartNew(IInvocationOperation invocation, Compilation compilation)
        {
            var targetMethod = invocation.TargetMethod;
            
            if (targetMethod.Name != "StartNew")
            {
                return false;
            }

            // Check if it's Task.Factory.StartNew
            if (isTaskFactoryType(targetMethod.ReceiverType, compilation))
            {
                return true;
            }

            return false;

            static bool isTaskFactoryType(ITypeSymbol? type, Compilation compilation)
            {
                // Ignoring generic TaskFactory<T>, because the type of the delegate
                // that is accepted by it is 'Func<int>', not 'Func<Task<int>>'.
                // SO the async delegates can't be used there!
                return type.IsClrType(compilation, typeof(TaskFactory));
            }
        }

        private static bool HasLongRunningOption(IInvocationOperation invocation)
        {
            // Look for TaskCreationOptions.LongRunning in any of the arguments
            foreach (var argument in invocation.Arguments)
            {
                if (ContainsLongRunningOption(argument.Value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsLongRunningOption(IOperation operation)
        {
            switch (operation)
            {
                case IFieldReferenceOperation fieldRef:
                    return fieldRef.Field.Name == "LongRunning" &&
                           fieldRef.Field.ContainingType?.Name == "TaskCreationOptions";

                case IBinaryOperation binaryOp when binaryOp.OperatorKind == BinaryOperatorKind.Or:
                    return ContainsLongRunningOption(binaryOp.LeftOperand) ||
                           ContainsLongRunningOption(binaryOp.RightOperand);

                case IConversionOperation conversion:
                    return ContainsLongRunningOption(conversion.Operand);

                default:
                    return false;
            }
        }

        private static bool IsAsyncDelegate(IOperation operation)
        {
            switch (operation)
            {
                case IAnonymousFunctionOperation anonymousFunction:
                    return anonymousFunction.Symbol.IsAsync;

                case IDelegateCreationOperation delegateCreation:
                    return IsAsyncDelegate(delegateCreation.Target);

                case IMethodReferenceOperation methodRef:
                    return methodRef.Method.IsAsync;

                default:
                    return false;
            }
        }

        private static bool IsAsyncLambdaOrDelegate(IAnonymousFunctionOperation anonymousFunction)
        {
            // Check if the lambda/delegate is declared as async
            if (anonymousFunction.Syntax is AnonymousMethodExpressionSyntax anonymousMethod)
            {
                return anonymousMethod.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword);
            }

            if (anonymousFunction.Syntax is LambdaExpressionSyntax lambda)
            {
                return lambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword);
            }

            return false;
        }

        private static bool IsAsyncMethodReference(IDelegateCreationOperation delegateCreation, OperationAnalysisContext context)
        {
            if (delegateCreation.Target is IMethodReferenceOperation methodRef)
            {
                return methodRef.Method.IsAsync;
            }

            return false;
        }
    }
}

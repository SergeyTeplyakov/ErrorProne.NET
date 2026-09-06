using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.CoreAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RecursiveCallAnalyzer : DiagnosticAnalyzer
    {
        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [DiagnosticDescriptors.EPC30];

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterOperationAction(AnalyzeMethodBody, OperationKind.MethodBody);
        }

        private static void AnalyzeMethodBody(OperationAnalysisContext context)
        {
            var method = (IMethodSymbol)context.ContainingSymbol;
            var methodBody = (IMethodBodyOperation)context.Operation;
            
            // Find all ref parameters that have been "touched" (modified or passed to other methods)
            var touchedRefParameters = GetTouchedRefParameters(methodBody, method);

            // Parameters that are mutated anywhere in the body cannot make a guard "invariant".
            var mutatedParameters = GetMutatedParameters(methodBody);
            
            foreach (var invocation in methodBody.Descendants().OfType<IInvocationOperation>())
            {
                // Calls inside a lambda or local function don't execute as part of this method's
                // immediate control flow, so they aren't unconditional recursion. See issue #318.
                if (IsInsideNestedFunction(invocation))
                {
                    continue;
                }

                // Check if all parameters are passed as-is
                // So Factorial(n - 1) should be totally fine!
                if (invocation.Arguments.Length == method.Parameters.Length &&
                    // Checking that the method is the same.
                    SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.OriginalDefinition, method.OriginalDefinition) &&

                    // Check if the method is being called on the same instance
                    // For instance methods, we need to check if the receiver is 'this' or implicit (null)
                    // For static methods, there's no instance to check
                    IsCalledOnSameInstance(invocation, method) &&

                    // Checking if the parameters are passed as is.
                    // It is possible to have a false positive here if the parameters are mutable.
                    // But it is a very rare case, so we will ignore it for now.
                    invocation.Arguments.Zip(method.Parameters, (arg, param) =>
                        arg.Value is IParameterReferenceOperation paramRef &&
                        SymbolEqualityComparer.Default.Equals(paramRef.Parameter, param)
                    ).All(b => b) &&
                    
                    // For ref parameters, check if they were touched before this call
                    // If any ref parameter was touched, don't warn
                    !HasTouchedRefParameterBeforeCall(invocation, method, touchedRefParameters) &&

                    // Only warn when the recursive call is guaranteed to be reached and to
                    // recurse forever. That means either:
                    //   * the call is unconditional (no branching could terminate the method first), or
                    //   * the call is guarded only by "invariant" conditions -- conditions composed
                    //     purely of unchanged value parameters and constants -- so taking the branch
                    //     once guarantees taking it forever (e.g. 'if (b) Foo(b);').
                    // Anything that could terminate the recursion (early returns, instance-state
                    // checks, mutated arguments, method calls in the guard, etc.) suppresses the
                    // diagnostic to avoid false positives.
                    ShouldReportRecursion(invocation, methodBody, mutatedParameters))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.EPC30,
                        invocation.Syntax.GetLocation(),
                        method.Name));
                }
            }
        }

        private static bool IsInsideNestedFunction(IOperation operation)
        {
            for (var parent = operation.Parent; parent != null; parent = parent.Parent)
            {
                if (parent is IAnonymousFunctionOperation or ILocalFunctionOperation)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns <c>true</c> when the recursive <paramref name="call"/> is guaranteed to be
        /// reached and to recurse forever: either it is reached unconditionally, or every branching
        /// construct enclosing it is an "invariant guard" (a condition that, once taken, is always
        /// taken because it depends only on unchanged value parameters and constants). Any statement
        /// before the call that could terminate or branch the method suppresses the diagnostic.
        /// </summary>
        private static bool ShouldReportRecursion(
            IInvocationOperation call,
            IMethodBodyOperation methodBody,
            HashSet<IParameterSymbol> mutatedParameters)
        {
            IOperation child = call;
            for (IOperation? parent = call.Parent; parent != null; parent = parent.Parent)
            {
                // The call is nested inside a branching construct. Only keep going if that construct
                // is an invariant guard that still guarantees infinite recursion.
                if (IsBranchingOperation(parent) && !IsInvariantGuard(parent, mutatedParameters))
                {
                    return false;
                }

                // A 'try' body executes unconditionally, so recursion directly in it is still
                // reachable. Only the 'catch'/'finally' paths are conditional, so suppress those.
                if (parent is ITryOperation tryOperation && !ReferenceEquals(child, tryOperation.Body))
                {
                    return false;
                }

                // For an enclosing block, any statement executed before the call that can terminate
                // or branch the method (e.g. 'if (x) return;') means the recursion might not happen.
                if (parent is IBlockOperation block)
                {
                    foreach (var statement in block.Operations)
                    {
                        if (ReferenceEquals(statement, child))
                        {
                            break;
                        }

                        if (ContainsTerminatingOrBranchingFlow(statement))
                        {
                            return false;
                        }
                    }
                }

                if (ReferenceEquals(parent, methodBody))
                {
                    break;
                }

                child = parent;
            }

            return true;
        }

        /// <summary>
        /// Branching constructs that, when an operation is nested within them, mean the operation
        /// does not execute unconditionally.
        /// </summary>
        private static bool IsBranchingOperation(IOperation operation)
        {
            return operation is
                IConditionalOperation or          // 'if' statement and '?:' ternary
                ISwitchOperation or
                ISwitchExpressionOperation or
                ILoopOperation or                 // for/foreach/while/do
                ICoalesceOperation or             // '??'
                IConditionalAccessOperation;      // '?.'
        }

        /// <summary>
        /// Returns <c>true</c> if taking the branch represented by <paramref name="branching"/> is
        /// guaranteed to be taken again on the next recursion, i.e. its condition is "invariant".
        /// Only simple <c>if</c>/ternary and top-tested <c>while</c> guards are considered; switches,
        /// loops with side effects, try/catch and null-conditional operators are never treated as
        /// invariant (we suppress to stay on the safe side).
        /// </summary>
        private static bool IsInvariantGuard(IOperation branching, HashSet<IParameterSymbol> mutatedParameters)
        {
            switch (branching)
            {
                case IConditionalOperation conditional:
                    return IsInvariantCondition(conditional.Condition, mutatedParameters);

                case IWhileLoopOperation { ConditionIsTop: true, ConditionIsUntil: false, Condition: { } condition }:
                    return IsInvariantCondition(condition, mutatedParameters);

                default:
                    return false;
            }
        }

        /// <summary>
        /// A condition is invariant when it is composed purely of references to unchanged value
        /// parameters, constants, comparisons and boolean/arithmetic operators. References to
        /// instance state, properties, locals, method calls, or mutated parameters make it
        /// non-invariant (so the recursion may terminate and we don't warn).
        /// </summary>
        private static bool IsInvariantCondition(IOperation condition, HashSet<IParameterSymbol> mutatedParameters)
        {
            foreach (var op in DescendantsAndSelf(condition))
            {
                if (!IsAllowedInvariantOperation(op, mutatedParameters))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAllowedInvariantOperation(IOperation operation, HashSet<IParameterSymbol> mutatedParameters)
        {
            switch (operation)
            {
                case IParameterReferenceOperation parameterReference:
                    // Only by-value parameters that are never mutated keep the condition invariant.
                    return parameterReference.Parameter.RefKind == RefKind.None &&
                           !mutatedParameters.Contains(parameterReference.Parameter);

                case ILiteralOperation:
                case IBinaryOperation:
                case IUnaryOperation:
                case IParenthesizedOperation:
                case IConversionOperation:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the statement subtree contains control flow that can terminate
        /// or branch the method (conditionals, switches, loops, try, return, throw, break,
        /// continue, goto).
        /// </summary>
        private static bool ContainsTerminatingOrBranchingFlow(IOperation statement)
        {
            foreach (var op in DescendantsAndSelf(statement))
            {
                if (op is
                    IConditionalOperation or
                    ISwitchOperation or
                    ISwitchExpressionOperation or
                    ILoopOperation or
                    ITryOperation or
                    IReturnOperation or
                    IThrowOperation or
                    IBranchOperation)             // break/continue/goto
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<IOperation> DescendantsAndSelf(IOperation operation)
        {
            yield return operation;
            foreach (var descendant in operation.Descendants())
            {
                yield return descendant;
            }
        }

        private static bool HasTouchedRefParameterBeforeCall(IInvocationOperation recursiveCall, IMethodSymbol method, HashSet<IParameterSymbol> touchedRefParameters)
        {
            // Check if any ref parameter in the recursive call was touched
            for (int i = 0; i < recursiveCall.Arguments.Length; i++)
            {
                var arg = recursiveCall.Arguments[i];
                var param = method.Parameters[i];
                
                // If this is a ref parameter and it's passed as-is, check if it was touched
                if (param.RefKind == RefKind.Ref && 
                    arg.Value is IParameterReferenceOperation paramRef &&
                    SymbolEqualityComparer.Default.Equals(paramRef.Parameter, param) &&
                    touchedRefParameters.Contains(param))
                {
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Returns every parameter that is mutated anywhere in the body: assigned to (including
        /// compound '+=', coalesce '??=', and deconstruction assignments), incremented/decremented,
        /// or passed by <c>ref</c>/<c>out</c> to another method.
        /// </summary>
        private static HashSet<IParameterSymbol> GetMutatedParameters(IMethodBodyOperation methodBody)
        {
            var mutated = new HashSet<IParameterSymbol>(SymbolEqualityComparer.Default);

            foreach (var op in methodBody.Descendants())
            {
                switch (op)
                {
                    // Deconstruction targets are tuples, e.g. '(n, x) = ...'; collect every
                    // parameter written by the deconstruction.
                    case IDeconstructionAssignmentOperation deconstruction:
                        foreach (var target in DescendantsAndSelf(deconstruction.Target))
                        {
                            if (target is IParameterReferenceOperation deconstructedParam)
                            {
                                mutated.Add(deconstructedParam.Parameter);
                            }
                        }
                        break;

                    // Covers simple '=', compound '+=' etc., and coalesce '??=' assignments.
                    case IAssignmentOperation { Target: IParameterReferenceOperation assignedParam }:
                        mutated.Add(assignedParam.Parameter);
                        break;

                    case IIncrementOrDecrementOperation { Target: IParameterReferenceOperation incrementedParam }:
                        mutated.Add(incrementedParam.Parameter);
                        break;

                    case IArgumentOperation { Value: IParameterReferenceOperation refArg } argument
                        when argument.Parameter?.RefKind is RefKind.Ref or RefKind.Out:
                        mutated.Add(refArg.Parameter);
                        break;
                }
            }

            return mutated;
        }

        private static HashSet<IParameterSymbol> GetTouchedRefParameters(IMethodBodyOperation methodBody, IMethodSymbol method)
        {
            var touchedParams = new HashSet<IParameterSymbol>(SymbolEqualityComparer.Default);
            
            // Look for assignments to ref parameters
            foreach (var assignment in methodBody.Descendants().OfType<IAssignmentOperation>())
            {
                if (assignment.Target is IParameterReferenceOperation paramRef &&
                    paramRef.Parameter.RefKind == RefKind.Ref)
                {
                    touchedParams.Add(paramRef.Parameter);
                }
            }
            
            // Look for ref parameters being passed to other methods
            foreach (var invocation in methodBody.Descendants().OfType<IInvocationOperation>())
            {
                // Skip the method itself to avoid checking recursive calls
                if (SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.OriginalDefinition, method.OriginalDefinition))
                {
                    continue;
                }
                
                foreach (var arg in invocation.Arguments)
                {
                    // Check if a ref parameter is being passed as ref/out to another method
                    if (arg.ArgumentKind == ArgumentKind.Explicit &&
                        (arg.Parameter?.RefKind == RefKind.Ref || arg.Parameter?.RefKind == RefKind.Out) &&
                        arg.Value is IParameterReferenceOperation paramRef &&
                        paramRef.Parameter.RefKind == RefKind.Ref)
                    {
                        touchedParams.Add(paramRef.Parameter);
                    }
                }
            }
            
            return touchedParams;
        }

        private static bool IsCalledOnSameInstance(IInvocationOperation invocation, IMethodSymbol containingMethod)
        {
            // For static methods, there's no instance to check, so any call to the same static method is recursive
            if (containingMethod.IsStatic)
            {
                return true;
            }

            // For instance methods, check if the receiver is 'this' (implicit or explicit)
            var receiver = invocation.Instance;
            
            // If receiver is null, it means it's an implicit 'this' call (e.g., just Foo() instead of this.Foo())
            if (receiver == null)
            {
                return true;
            }

            // If receiver is an explicit 'this' reference
            if (receiver is IInstanceReferenceOperation instanceRef && 
                instanceRef.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance)
            {
                return true;
            }

            // Any other receiver (like Parent?.Foo()) means it's called on a different instance
            return false;
        }
    }
}

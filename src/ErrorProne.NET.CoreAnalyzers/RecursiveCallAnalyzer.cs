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
            
            foreach (var invocation in methodBody.Descendants().OfType<IInvocationOperation>())
            {
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
                    !HasTouchedRefParameterBeforeCall(invocation, method, touchedRefParameters))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.EPC30,
                        invocation.Syntax.GetLocation(),
                        method.Name));
                }
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

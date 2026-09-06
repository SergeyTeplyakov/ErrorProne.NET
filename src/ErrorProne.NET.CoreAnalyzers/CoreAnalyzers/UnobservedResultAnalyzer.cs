using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// Analyzer that warns when the result of a method invocation is ignore (when it potentially, shouldn't).
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnobservedResultAnalyzer : UnobservedResultAnalyzerBase
    {
        private static DiagnosticDescriptor Rule => DiagnosticDescriptors.EPC13;

        /// <nodoc />
        public UnobservedResultAnalyzer() 
            : base(Rule)
        {
        }

        protected override bool ShouldAnalyzeMethod(IMethodSymbol method, Compilation compilation)
        {
            return TypeMustBeObserved(method.ReturnType, method, compilation);
        }

        protected override Diagnostic CreateDiagnostic(IInvocationOperation invocation)
        {
            var location = GetLocationForDiagnostic(invocation);
            return Diagnostic.Create(Rule, location, invocation.TargetMethod.ReturnType.Name);
        }

        protected override Diagnostic CreateAwaitDiagnostic(IAwaitOperation awaitOperation)
        {
            return Diagnostic.Create(Rule, awaitOperation.Syntax.GetLocation(), awaitOperation.Type!.Name);
        }

        protected override bool ShouldAnalyzeAwaitedResult(ITypeSymbol type, IMethodSymbol? method, Compilation compilation)
        {
            return TypeMustBeObserved(type, method, compilation);
        }

        private static Location GetLocationForDiagnostic(IInvocationOperation invocation)
        {
            // Try to get the best location for the diagnostic
            var syntax = invocation.Syntax;
            if (syntax is Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax invocationSyntax)
            {
                var simpleMemberAccess = invocationSyntax.Expression as Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax;
                return (simpleMemberAccess?.Name ?? invocationSyntax.Expression).GetLocation();
            }
            
            return syntax.GetLocation();
        }

        private static bool TypeMustBeObserved(ITypeSymbol type, IMethodSymbol? method, Compilation compilation)
        {
            if (method?.IsContinueWith(compilation) == true)
            {
                // Task.ContinueWith is a bit special.
                return false;
            }

            return type.EnumerateBaseTypesAndSelf().Any(t => IsObservableType(t, method, compilation));
        }

        private static bool IsObservableType(ITypeSymbol type, IMethodSymbol? method, Compilation compilation)
        {
            if (type.IsClrType(compilation, typeof(Exception)))
            {
                // 'ThrowException' method that throws but still returns an exception is quite common.
                var methodName = method?.Name;
                if (methodName == null)
                {
                    return false;
                }

                if (methodName.StartsWith("Throw") || methodName == "FailFast")
                {
                    return false;
                }
                
                return true;
            }

            if (type.IsClrType(compilation, typeof(Task)))
            {
                // Tasks should be observed
                return true;
            }

            if (type.Name.StartsWith("Result") || type.Name.StartsWith("ResultBase") ||
                type.Name.StartsWith("Possible"))
            {
                return true;
            }

            return false;
        }
    }
}
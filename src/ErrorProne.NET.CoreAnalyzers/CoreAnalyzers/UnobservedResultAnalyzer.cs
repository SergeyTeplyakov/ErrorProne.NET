using System;
using System.Collections.Generic;
using System.Diagnostics.ContractsLight;
using System.Linq;
using System.Threading.Tasks;
using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.Core.CoreAnalyzers
{
    /// <summary>
    /// Analyzer that warns when the result of a method invocation is ignore (when it potentially, shouldn't).
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnobservedResultAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.UnobservedResult;

        private static readonly string Title = "Suspiciously unobserved result.";
        private static readonly string Message = "Result of type '{0}' should better be observed.";

        private static readonly string Description = "Result for some methods should better be observed";
        private const string Category = "CodeSmell";

        // Using warning for visibility purposes
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        private static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, Message, Category, Severity, isEnabledByDefault: true, description: Description);

        /// <nodoc />
        public UnobservedResultAnalyzer() 
            : base(Rule)
        {
        }

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeAwaitExpression, SyntaxKind.AwaitExpression);
            context.RegisterSyntaxNodeAction(AnalyzeMethodInvocation, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeMethodInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            if (invocation.Parent is ExpressionStatementSyntax ex &&
                context.SemanticModel.GetSymbolInfo(ex.Expression).Symbol is IMethodSymbol ms && 
                TypeMustBeObserved(ms.ReturnType, ms, context.Compilation))
            {
                var operation = context.SemanticModel.GetOperation(invocation);
                var invocationOperation = operation as IInvocationOperation;
                if (invocationOperation != null &&
                    ResultObservedByExtensionMethod(invocationOperation, context.SemanticModel))
                {
                    // Result is observed!
                    return;
                }

                if (invocationOperation != null && IsException(invocationOperation.Type, context.Compilation) &&
                    invocationOperation.TargetMethod.ConstructedFrom.ReturnType is ITypeParameterSymbol)
                {
                    // The inferred type is System.Exception (or one of it's derived types),
                    // but the operation that is called is generic (i.e. exception type was inferred).
                    // It means that there is nothing special about the return type and it can be ignored.
                    return;
                }

                var diagnostic = Diagnostic.Create(Rule, invocation.GetNodeLocationForDiagnostic(), ms.ReturnType.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void AnalyzeAwaitExpression(SyntaxNodeAnalysisContext context)
        {
            var awaitExpression = (AwaitExpressionSyntax)context.Node;

            // await can be used on a task value, so the awaited expression may be anything.
            if (awaitExpression.Parent is ExpressionStatementSyntax ex)
            {
                var operation = context.SemanticModel.GetOperation(awaitExpression);
                if (operation is IAwaitOperation awaitOperation && operation.Type != null && TypeMustBeObserved(operation.Type, null, context.Compilation))
                {
                    if (awaitOperation.Operation is IInvocationOperation invocation &&
                        ResultObservedByExtensionMethod(invocation, context.SemanticModel))
                    {
                        // Result is observed!
                        return;
                    }

                    // Need to extract a real method if this one is 'ConfigureAwait'
                    var location = GetLocationForDiagnostic(awaitExpression);

                    var diagnostic = Diagnostic.Create(Rule, location, operation.Type.Name);
                    ReportDiagnostic(context, diagnostic);
                }
            }
        }

        private bool ResultObservedByExtensionMethod(IInvocationOperation operation, SemanticModel semanticModel)
        {
            // In some cases, the following pattern is used:
            // Foo().Handle();
            // Where Foo() returns 'possible' that is passed to 'Handle' extension method that returns the result.
            // But in this case we can safely assume that the result WAS observed.

            var methodSymbol = operation.TargetMethod;

            // Exception for this rule is 'ConfigureAwait()'
            if (operation.TargetMethod.IsConfigureAwait(semanticModel.Compilation))
            {
                return false;
            }

            // First, checking that method that is called is an extension method that takes the result.
            if (methodSymbol.IsExtensionMethod &&
                (methodSymbol.ReturnType.Equals(methodSymbol.ReceiverType) ||
                 methodSymbol.ReturnType.Equals(methodSymbol.Parameters.FirstOrDefault()?.Type)))
            {
                // operation.Type returns a type for 'Foo()'.
                return operation.Type.Equals(methodSymbol.ReturnType);
            }

            return false;
        }

        private Location GetLocationForDiagnostic(AwaitExpressionSyntax awaitExpression)
        {
            return awaitExpression.GetLocation();
        }

        private static void ReportDiagnostic(SyntaxNodeAnalysisContext context, Diagnostic diagnostic)
        {
#if DEBUG
            Console.WriteLine($"ERROR: {diagnostic}");
#endif
            context.ReportDiagnostic(diagnostic);
        }

        private bool TypeMustBeObserved(ITypeSymbol type, /*CanBeNull*/IMethodSymbol method, Compilation compilation)
        {
            if (method?.IsContinueWith(compilation) == true)
            {
                // Task.ContinueWith is a bit special.
                return false;
            }

            return EnumerateBaseTypesAndSelf(type).Any(t => IsObserableType(t, method, compilation));
        }

        private static bool IsObserableType(ITypeSymbol type, /*CanBeNull*/IMethodSymbol method, Compilation compilation)
        {
            if (type.IsClrType(compilation, typeof(Exception)))
            {
                // 'ThrowExcpetion' method that throws but still returns an exception is quite common.
                if (method?.Name.StartsWith("Throw") == true || method?.Name == "FailFast")
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

        private static bool IsException(ITypeSymbol type, Compilation compilation)
        {
            return EnumerateBaseTypesAndSelf(type).Any(t => t.IsClrType(compilation, typeof(Exception)));
        }

        public static IEnumerable<ITypeSymbol> EnumerateBaseTypesAndSelf(ITypeSymbol type)
        {
            while (type != null)
            {
                yield return type;
                type = type.BaseType;
            }
        }
    }

    public static class InvocationExpressionExtensions
    {
        public static Location GetNodeLocationForDiagnostic(this InvocationExpressionSyntax invocationExpression)
        {
            Contract.Requires(invocationExpression != null);
            var simpleMemberAccess = invocationExpression.Expression as MemberAccessExpressionSyntax;
            return (simpleMemberAccess?.Name ?? invocationExpression.Expression).GetLocation();
        }
    }

}
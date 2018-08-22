using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
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

            // TODO: different error message should be used for UseReturnValueAttribute
            if (invocation.Parent is ExpressionStatementSyntax ex &&
                context.SemanticModel.GetSymbolInfo(ex.Expression).Symbol is IMethodSymbol ms && 
                TypeMustBeObserved(ms.ReturnType, ms, context.Compilation))
            {
                if (!ResultObservedByExtensionMethod(invocation, ms, context.SemanticModel))
                {
                    var diagnostic = Diagnostic.Create(Rule, invocation.GetNodeLocationForDiagnostic(), ms.ReturnType.Name);
                    context.ReportDiagnostic(diagnostic);
                }
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
                        ResultObservedByExtensionMethod(
                            invocation.Syntax as InvocationExpressionSyntax,
                            invocation.TargetMethod,
                            context.SemanticModel))
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

        private bool ResultObservedByExtensionMethod(InvocationExpressionSyntax methodCall, IMethodSymbol methodSymbol, SemanticModel semanticModel)
        {
            if (methodCall == null)
            {
                return false;
            }

            // In some cases, the following pattern is used:
            // Foo().Handle();
            // Where Foo() returns 'possible' that is passed to 'Handle' extension method that returns the result.
            // But in this case we can safely assume that the result WAS observed.

            // Exception for this rule is 'ConfigureAwait()'
            if (methodSymbol.IsConfigureAwait(semanticModel.Compilation))
            {
                return false;
            }

            // First, checking that method that is called is an extension method that takes the result.
            if (methodSymbol.IsExtensionMethod &&
                (methodSymbol.ReturnType.Equals(methodSymbol.ReceiverType) ||
                 methodSymbol.ReturnType.Equals(methodSymbol.Parameters.FirstOrDefault()?.Type)))
            {
                // Looking for 'Foo' in the example provided above
                if (methodCall.Expression is MemberAccessExpressionSyntax mae &&
                    mae.Expression is InvocationExpressionSyntax &&
                    semanticModel.GetSymbolInfo(mae.Expression).Symbol is IMethodSymbol nestedMethod)
                {
                    return nestedMethod.ReturnType.Equals(methodSymbol.ReturnType);
                }
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

        private bool TypeMustBeObserved(ITypeSymbol type, /*CanBeNull*/IMethodSymbol ms, Compilation compilation)
        {
            if (ms?.IsContinueWith(compilation) == true)
            {
                // Task.ContinueWith is a bit special.
                return false;
            }

            return EnumerateBaseTypesAndSelf(type).Any(t => IsObserableType(t, compilation));
        }

        private bool IsObserableType(ITypeSymbol type, Compilation compilation)
        {
            if (type.IsClrType(compilation, typeof(Exception)) || type.IsClrType(compilation, typeof(Task)))
            {
                // Exceptions and tasks are special
                return true;
            }

            if (type.Name.StartsWith("Result") || type.Name.StartsWith("ResultBase") ||
                type.Name.StartsWith("Possible"))
            {
                return true;
            }

            return false;
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
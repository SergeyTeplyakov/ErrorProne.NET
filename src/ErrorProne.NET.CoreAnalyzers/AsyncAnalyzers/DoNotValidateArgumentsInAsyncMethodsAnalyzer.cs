using System;
using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.AsyncAnalyzers
{
    /// <summary>
    /// Analyzer that warns when async methods validate arguments and throw exceptions.
    /// Such validation is not eager and exceptions are thrown when the task is awaited.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DoNotValidateArgumentsInAsyncMethodsAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = DiagnosticDescriptors.EPC37;

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterOperationAction(AnalyzeThrow, OperationKind.Throw);
            context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        }

        private static void AnalyzeThrow(OperationAnalysisContext context)
        {
            var throwOperation = (IThrowOperation)context.Operation;
            var method = GetEnclosingMethod(throwOperation, context);
            
            if (method == null || !ShouldAnalyze(method))
            {
                return;
            }

            // Check if this is argument validation (throw in the beginning of the method)
            if (IsArgumentValidationThrow(throwOperation, method))
            {
                ReportDiagnostic(context, throwOperation.Syntax.GetLocation(), method.Name);
            }
        }

        private static void AnalyzeInvocation(OperationAnalysisContext context)
        {
            var invocation = (IInvocationOperation)context.Operation;
            var method = GetEnclosingMethod(invocation, context);
            
            if (method == null || !ShouldAnalyze(method))
            {
                return;
            }

            // Check for ArgumentNullException.ThrowIfNull and similar methods
            if (IsArgumentValidationInvocation(invocation))
            {
                ReportDiagnostic(context, invocation.Syntax.GetLocation(), method.Name);
            }
        }

        private static IMethodSymbol? GetEnclosingMethod(IOperation operation, OperationAnalysisContext context)
        {
            if (operation.FindParentLocalOrLambdaSymbol() != null)
            {
                // We don't want to check inside local functions or lambdas
                return null;
            }

            return context.ContainingSymbol as IMethodSymbol;
        }

        private static bool ShouldAnalyze(IMethodSymbol method)
        {
            // Only analyze async methods
            if (!method.IsAsync)
            {
                return false;
            }

            // Only analyze public methods in public types
            if (method.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }

            var type = method.ContainingType;
            while (type != null)
            {
                if (type.DeclaredAccessibility != Accessibility.Public)
                {
                    return false;
                }
                type = type.ContainingType;
            }

            return true;
        }

        private static bool IsArgumentValidationThrow(IThrowOperation throwOperation, IMethodSymbol method)
        {
            // Check if the throw is in the beginning of the method (before any await)
            if (!IsInMethodBeginning(throwOperation, method))
            {
                return false;
            }

            // Check if it's throwing argument-related exceptions
            var thrownException = throwOperation.Exception;
            if (thrownException is IConversionOperation conversion)
            {
                if (conversion.Operand is IObjectCreationOperation objectCreation)
                {
                    // throw new ArgumentException("message", "paramName");
                    return IsArgumentException(objectCreation.Type);
                }

                if (conversion.Operand is IInvocationOperation invocation)
                {
                    // throw CreateArgumentException("message", "paramName");
                    return IsArgumentException(invocation.TargetMethod.ReturnType);
                }
            }

            return false;
        }

        private static bool IsArgumentValidationInvocation(IInvocationOperation invocation)
        {
            var targetMethod = invocation.TargetMethod;
            
            // Checking for 'ThrowIf' methods

            if (!targetMethod.Name.StartsWith("ThrowIf"))
            {
                return false;
            }

            // This is 'ThrowIfNull' or similar methods

            var typeName = targetMethod.ContainingType?.Name;
            return typeName is nameof(ArgumentNullException) or nameof(ArgumentException) or nameof(ArgumentOutOfRangeException);

            // This potentially can be extended in the future by providing a list of validation methods
            // inside .editorconfig to support custom validation methods.
            return false;
        }

        private static bool IsInMethodBeginning(IOperation operation, IMethodSymbol method)
        {
            // Check if there are any await operations before this operation in the method
            var methodSyntax = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax;
            if (methodSyntax?.Body == null)
            {
                return false;
            }

            var operationSyntax = operation.Syntax;
            var awaitExpressions = methodSyntax.Body.DescendantNodes()
                .OfType<AwaitExpressionSyntax>()
                .Where(await => await.SpanStart < operationSyntax.SpanStart);

            return !awaitExpressions.Any();
        }

        private static bool IsArgumentException(ITypeSymbol? exceptionType)
        {
            if (exceptionType == null)
            {
                return false;
            }

            var typeName = exceptionType.Name;
            return typeName == nameof(ArgumentException) ||
                   typeName == nameof(ArgumentNullException) ||
                   typeName == nameof(ArgumentOutOfRangeException) ||
                   IsInheritedFromArgumentException(exceptionType);
        }

        private static bool IsInheritedFromArgumentException(ITypeSymbol exceptionType)
        {
            var baseType = exceptionType.BaseType;
            while (baseType != null)
            {
                if (baseType.Name == nameof(ArgumentException))
                {
                    return true;
                }
                baseType = baseType.BaseType;
            }
            return false;
        }

        private static void ReportDiagnostic(OperationAnalysisContext context, Location location, string methodName)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, methodName));
        }
    }
}

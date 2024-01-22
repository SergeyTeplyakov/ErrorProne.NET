using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// Analyzer that warns when the result of a method invocation is ignore (when it potentially, shouldn't).
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnobservedResultAnalyzer : DiagnosticAnalyzer
    {
        private sealed class UnobservedResultAnalyzerInfo
        {
            public ImmutableArray<IMethodSymbol> ConfigureAwaitMethods { get; }
            public ImmutableHashSet<IMethodSymbol> ContinueWithMethods { get; }
            public INamedTypeSymbol? ExceptionSymbol { get; }
            public TaskTypesInfo TaskTypesInfo { get; }

            public UnobservedResultAnalyzerInfo(Compilation compilation)
            {
                TaskTypesInfo = new TaskTypesInfo(compilation);
                ExceptionSymbol = compilation.GetTypeByMetadataName(typeof(Exception).FullName);
                ConfigureAwaitMethods = TaskTypesInfo.TaskSymbol?.GetMembers("ConfigureAwait").OfType<IMethodSymbol>().ToImmutableArray() ?? ImmutableArray<IMethodSymbol>.Empty;
#pragma warning disable RS1024 // Symbols should be compared for equality
                ContinueWithMethods = TaskTypesInfo.TaskSymbol?.GetMembers("ContinueWith").OfType<IMethodSymbol>().ToImmutableHashSet() ?? ImmutableHashSet<IMethodSymbol>.Empty;
#pragma warning restore RS1024 // Symbols should be compared for equality
            }
        }

        private static DiagnosticDescriptor Rule => DiagnosticDescriptors.EPC13;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <nodoc />
        public UnobservedResultAnalyzer() 
            //: base(supportFading: false, diagnostics: Rule)
        {
        }

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(context =>
            {
                var compilation = context.Compilation;

                var info = new UnobservedResultAnalyzerInfo(compilation);

                context.RegisterOperationAction(context => AnalyzeAwaitOperation(context, info), OperationKind.Await);
                context.RegisterOperationAction(context => AnalyzeMethodInvocation(context, info), OperationKind.Invocation);
            });
        }

        private static void AnalyzeMethodInvocation(OperationAnalysisContext context, UnobservedResultAnalyzerInfo info)
        {
            var invocationOperation = (IInvocationOperation)context.Operation;
            var invocation = (InvocationExpressionSyntax)invocationOperation.Syntax;
            if (invocationOperation.Parent is IExpressionStatementOperation &&
                TypeMustBeObserved(invocationOperation.TargetMethod.ReturnType, invocationOperation.TargetMethod, info))
            {
                if (ResultObservedByExtensionMethod(invocationOperation, info))
                {
                    // Result is observed!
                    return;
                }

                if (IsException(invocationOperation.Type, info) &&
                    invocationOperation.TargetMethod.ConstructedFrom.ReturnType is ITypeParameterSymbol)
                {
                    // The inferred type is System.Exception (or one of it's derived types),
                    // but the operation that is called is generic (i.e. exception type was inferred).
                    // It means that there is nothing special about the return type and it can be ignored.
                    return;
                }

                var diagnostic = Diagnostic.Create(Rule, invocation.GetNodeLocationForDiagnostic(), invocationOperation.TargetMethod.ReturnType.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static void AnalyzeAwaitOperation(OperationAnalysisContext context, UnobservedResultAnalyzerInfo info)
        {
            var awaitOperation = (IAwaitOperation)context.Operation;

            // await can be used on a task value, so the awaited expression may be anything.
            if (awaitOperation.Parent is IExpressionStatementOperation)
            {
                if (awaitOperation.Type != null && TypeMustBeObserved(awaitOperation.Type, null, info))
                {
                    if (awaitOperation.Operation is IInvocationOperation invocation &&
                        ResultObservedByExtensionMethod(invocation, info))
                    {
                        // Result is observed!
                        return;
                    }

                    // Making an exception for 'Task<Task>' case.
                    // For instance, the following code is totally fine: await Task.WhenAll(t1, t2);
                    if (awaitOperation.Type.IsTaskLike(info.TaskTypesInfo))
                    {
                        return;
                    }

                    // Need to extract a real method if this one is 'ConfigureAwait'
                    var location = GetLocationForDiagnostic((AwaitExpressionSyntax)awaitOperation.Syntax);

                    var diagnostic = Diagnostic.Create(Rule, location, awaitOperation.Type.Name);
                    ReportDiagnostic(context, diagnostic);
                }
            }
        }

        private static bool ResultObservedByExtensionMethod(IInvocationOperation operation, UnobservedResultAnalyzerInfo info)
        {
            // In some cases, the following pattern is used:
            // Foo().Handle();
            // Where Foo() returns 'possible' that is passed to 'Handle' extension method that returns the result.
            // But in this case we can safely assume that the result WAS observed.

            var methodSymbol = operation.TargetMethod;

            // Exception for this rule is 'ConfigureAwait()'
            if (info.ConfigureAwaitMethods.Contains(methodSymbol))
            {
                return false;
            }

            // First, checking that method that is called is an extension method that takes the result.
            if (methodSymbol.IsExtensionMethod &&
                (methodSymbol.ReturnType.Equals(methodSymbol.ReceiverType, SymbolEqualityComparer.Default) ||
                 methodSymbol.ReturnType.Equals(methodSymbol.Parameters.FirstOrDefault()?.Type, SymbolEqualityComparer.Default)))
            {
                // operation.Type returns a type for 'Foo()'.
                return operation.Type?.Equals(methodSymbol.ReturnType, SymbolEqualityComparer.Default) == true;
            }

            return false;
        }

        private static Location GetLocationForDiagnostic(AwaitExpressionSyntax awaitExpression)
        {
            return awaitExpression.GetLocation();
        }

        private static void ReportDiagnostic(OperationAnalysisContext context, Diagnostic diagnostic)
        {
#if DEBUG
            Console.WriteLine($"ERROR: {diagnostic}");
#endif
            context.ReportDiagnostic(diagnostic);
        }

        private static bool TypeMustBeObserved(ITypeSymbol type, IMethodSymbol? method, UnobservedResultAnalyzerInfo info)
        {
            if (method is not null && info.ContinueWithMethods.Contains(method))
            {
                // Task.ContinueWith is a bit special.
                return false;
            }

            return type.EnumerateBaseTypesAndSelf().Any(t => IsObservableType(t, method, info));
        }

        private static bool IsObservableType(ITypeSymbol type, IMethodSymbol? method, UnobservedResultAnalyzerInfo info)
        {
            if (type.Equals(info.ExceptionSymbol, SymbolEqualityComparer.Default))
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

            if (type.Equals(info.TaskTypesInfo.TaskSymbol, SymbolEqualityComparer.Default))
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

        private static bool IsException(ITypeSymbol? type, UnobservedResultAnalyzerInfo info)
        {
            return type.EnumerateBaseTypesAndSelf().Any(t => t.Equals(info.ExceptionSymbol, SymbolEqualityComparer.Default));
        }
    }

    public static class InvocationExpressionExtensions
    {
        public static Location GetNodeLocationForDiagnostic(this InvocationExpressionSyntax invocationExpression)
        {
            var simpleMemberAccess = invocationExpression.Expression as MemberAccessExpressionSyntax;
            return (simpleMemberAccess?.Name ?? invocationExpression.Expression).GetLocation();
        }
    }
}
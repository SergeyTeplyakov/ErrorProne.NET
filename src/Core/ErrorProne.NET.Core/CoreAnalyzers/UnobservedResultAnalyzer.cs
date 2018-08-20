using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

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
            new DiagnosticDescriptor(DiagnosticId, Title, Title, Category, Severity, isEnabledByDefault: true, description: Description);

        /// <nodoc />
        public UnobservedResultAnalyzer() 
            : base(Rule)
        {
        }

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeMethodInvocation, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeMethodInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            // TODO: different error message should be used for UseReturnValueAttribute
            if (invocation.Parent is ExpressionStatementSyntax ex &&
                context.SemanticModel.GetSymbolInfo(ex.Expression).Symbol is IMethodSymbol ms && 
                TypeMustBeObserved(ms.ReturnType, context.Compilation))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetNodeLocationForDiagnostic()));
            }
        }

        private bool TypeMustBeObserved(ITypeSymbol type, Compilation compilation)
        {
            return EnumerateBaseTypesAndSelf(type).Any(t => IsObserableType(t, compilation));
        }

        private bool IsObserableType(ITypeSymbol type, Compilation compilation)
        {
            if (type.IsClrType(compilation, typeof(Exception)))
            {
                // Exceptions as special
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
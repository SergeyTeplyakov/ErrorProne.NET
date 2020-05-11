using System;
using System.Collections.Immutable;
using System.Diagnostics.ContractsLight;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.StructAnalyzers
{
    /// <summary>
    /// An analyzer that warns when the compiler copies a struct behind the scene.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class HiddenStructCopyAnalyzer : DiagnosticAnalyzer
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.HiddenStructCopyDiagnosticId;

        private const string Title = "Hidden struct copy operation";
        private const string MessageFormat = "An expression '{0}' causes a hidden copy of a {2}struct '{1}' of estimated size '{3}'";
        private const string Description = "The compiler emits a defensive copy to make sure a struct instance remains unchanged";
        private const string Category = "Performance";
        
        // Using warning for visibility purposes
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        public static readonly DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);
        
        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterSyntaxNodeAction(AnalyzeDottedExpression, SyntaxKind.SimpleMemberAccessExpression);
            context.RegisterSyntaxNodeAction(AnalyzeElementAccessExpression, SyntaxKind.ElementAccessExpression);
        }

        private void AnalyzeDottedExpression(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is MemberAccessExpressionSyntax ma &&
                // In a case of 'a.b.c' we need to analyzer 'a.b' case and skip everything else
                // to avoid incorrect results.
                !(ma.Expression is MemberAccessExpressionSyntax))
            {
                var targetSymbol = context.SemanticModel.GetSymbolInfo(ma).Symbol;
                AnalyzeExpressionAndTargetSymbol(context, ma.Expression, ma.Name, targetSymbol);
            }
        }

        private void AnalyzeElementAccessExpression(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is ElementAccessExpressionSyntax ea)
            {
                var targetSymbol = context.SemanticModel.GetSymbolInfo(ea).Symbol;
                AnalyzeExpressionAndTargetSymbol(context, ea.Expression, null, targetSymbol: targetSymbol);
            }
        }

        private static RefKind GetExtensionMethodThisRefKind(IMethodSymbol method)
        {
            Contract.Requires(method.IsExtensionMethod);

            // The logic is quite tricky because it depends on the call form:
            // If an extension method is called using foo.Extension() then the parameter
            // should be obtained via 'ReducedFrom', otherwise ReducedFrom is null
            // and Parameters property should be used.

            if (method.ReducedFrom != null)
            {
                return method.ReducedFrom.Parameters[0].RefKind;
            }

            return method.Parameters[0].RefKind;
        }

        private static void AnalyzeExpressionAndTargetSymbol(
            SyntaxNodeAnalysisContext context,
            ExpressionSyntax expression,
            SimpleNameSyntax? name,
            ISymbol targetSymbol)
        {
            if (targetSymbol is IMethodSymbol ms && ms.IsExtensionMethod && 
                GetExtensionMethodThisRefKind(ms) == RefKind.None &&
                ms.ReceiverType.IsLargeStruct(context.Compilation, Settings.LargeStructThreshold, out var estimatedSize))
            {
                // The expression calls an extension method that takes a struct by value.
                ReportDiagnostic(context, name ?? expression, ms.ReceiverType, estimatedSize);
            }

            var symbol = context.SemanticModel.GetSymbolInfo(expression).Symbol;
            if (symbol is IFieldSymbol fs && fs.IsReadOnly)
            {
                // The expression uses readonly field of non-readonly struct
                ReportDiagnosticIfTargetIsNotField(context, name ?? expression, fs.Type, targetSymbol);
            }
            else if (symbol is IParameterSymbol p && p.RefKind == RefKind.In)
            {
                // The expression uses in-parameter
                ReportDiagnosticIfTargetIsNotField(context, name ?? expression, p.Type, targetSymbol);
            }
            else if (symbol is ILocalSymbol ls && ls.IsRef && ls.RefKind == RefKind.In)
            {
                // The expression uses ref readonly local
                ReportDiagnosticIfTargetIsNotField(context, name ?? expression, ls.Type, targetSymbol);
            }
            else if (symbol is IMethodSymbol method && method.ReturnsByRefReadonly)
            {
                // The expression uses ref readonly return
                ReportDiagnosticIfTargetIsNotField(context, name ?? expression, method.ReturnType, targetSymbol);
            }
        }

        private static void ReportDiagnosticIfTargetIsNotField(SyntaxNodeAnalysisContext context,
            ExpressionSyntax expression, ITypeSymbol resolvedType, ISymbol targetSymbol)
        {
            if (targetSymbol != null && 
                !(targetSymbol is IFieldSymbol) &&
                resolvedType.IsValueType &&
                // Dispose method is special: ignoring them
                !targetSymbol.IsDisposeMethod() &&
                resolvedType.TypeKind != TypeKind.Enum &&
                // Skipping access to nullable type properties.
                !resolvedType.IsNullableType() &&
                !resolvedType.IsReadOnlyStruct() &&
                // Warn only when the size of the struct is larger then threshold
                resolvedType.IsLargeStruct(context.Compilation, Settings.LargeStructThreshold, out var estimatedSize))
            {
                // This is not a field, emit a warning because this property access will cause
                // a defensive copy.
                ReportDiagnostic(context, expression, resolvedType, estimatedSize, "non-readonly ");
            }
        }

        private static void ReportDiagnostic(
            SyntaxNodeAnalysisContext context, ExpressionSyntax expression, ITypeSymbol resolvedType,
            int estimatedSize,
            string? modifier = null)
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                expression.GetLocation(),
                expression.ToFullString(),
                resolvedType.ToDisplayString(),
                modifier ?? string.Empty,
                estimatedSize);

            context.ReportDiagnostic(diagnostic);
        }
    }
}
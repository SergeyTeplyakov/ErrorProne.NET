using System;
using System.Collections.Immutable;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.StructAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UseInModifierForReadOnlyStructAnalyzer : DiagnosticAnalyzer
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.UseInModifierForReadOnlyStructDiagnosticId;

        private const string Title = "Use in-modifier for a readonly struct";
        private const string MessageFormat = "Use in-modifier for passing a readonly struct '{0}' of estimated size '{1}'";
        private const string Description = "Readonly structs have better performance when passed readonly reference";
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

            context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
            context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
        }

        private void AnalyzeNamedType(SymbolAnalysisContext context)
        {
            var symbol = (INamedTypeSymbol)context.Symbol;
            if (symbol.TypeKind != TypeKind.Delegate)
            {
                return;
            }

            if (symbol.DelegateInvokeMethod is null || symbol.DelegateInvokeMethod.Parameters.IsEmpty)
            {
                // No need to do any work if there are no parameters
                return;
            }

            var largeStructThreshold = Settings.GetLargeStructThresholdOrDefault(context.TryGetAnalyzerConfigOptions());
            foreach (var parameterSymbol in symbol.DelegateInvokeMethod.Parameters)
            {
                WarnIfParameterIsReadOnly(context.Compilation, largeStructThreshold, parameterSymbol, diagnostic => context.ReportDiagnostic(diagnostic));
            }
        }

        private void AnalyzeMethod(SymbolAnalysisContext context)
        {
            var method = (IMethodSymbol) context.Symbol;
            if (IsOverridenMethod(method) || method.IsAsyncOrTaskBased(context.Compilation) || method.IsIteratorBlock())
            {
                // If the method overrides a base method or implements an interface,
                // then we can't enforce 'in'-modifier
                return;
            }

            var largeStructThreshold = Settings.GetLargeStructThresholdOrDefault(context.TryGetAnalyzerConfigOptions());

            // Should analyze only subset of methods, not all of them.
            // What about operators?
            if (method.MethodKind == MethodKind.Ordinary || method.MethodKind == MethodKind.AnonymousFunction ||
                method.MethodKind == MethodKind.LambdaMethod || method.MethodKind == MethodKind.LocalFunction ||
                method.MethodKind == MethodKind.PropertyGet)
            {
                foreach (var p in method.Parameters)
                {
                    WarnIfParameterIsReadOnly(context.Compilation, largeStructThreshold, p, diagnostic => context.ReportDiagnostic(diagnostic));
                }
            }
        }

        private static bool IsOverridenMethod(IMethodSymbol method)
        {
            return method.IsOverride || 
                   method.IsInterfaceImplementation();   
        }

        private static void WarnIfParameterIsReadOnly(Compilation compilation, int largeStructThreshold, IParameterSymbol p, Action<Diagnostic> diagnosticReporter)
        {
            if (p.RefKind == RefKind.None && p.Type.IsReadOnlyStruct() && p.Type.IsLargeStruct(compilation, largeStructThreshold, out var estimatedSize))
            {
                Location location = p.GetParameterLocation();

                var diagnostic = Diagnostic.Create(Rule, location, p.Type.ToDisplayString(), estimatedSize);

                diagnosticReporter(diagnostic);
            }
        }
    }
}
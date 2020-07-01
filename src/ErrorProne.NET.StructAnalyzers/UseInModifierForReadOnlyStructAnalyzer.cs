using System;
using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        // Using info to avoid too much noise from the analyzer
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Info;

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
            
            context.RegisterCompilationStartAction(context =>
            {
                var structSizeCalculator = new StructSizeCalculator(context.Compilation);
                context.RegisterSymbolAction(analysisContext => AnalyzeNamedType(analysisContext, structSizeCalculator), SymbolKind.NamedType);
                context.RegisterSymbolAction(analysisContext => AnalyzeMethod(analysisContext, structSizeCalculator), SymbolKind.Method);
            });
        }

        private static void AnalyzeNamedType(SymbolAnalysisContext context, StructSizeCalculator structSizeCalculator)
        {
            var symbol = (INamedTypeSymbol)context.Symbol;
            if (symbol.TypeKind != TypeKind.Delegate)
            {
                // We're interested only in 'delegate void FooBar(MyStruct s);' cases.
                return;
            }

            if (symbol.DelegateInvokeMethod is null || symbol.DelegateInvokeMethod.Parameters.IsEmpty)
            {
                // No need to do any work if there are no parameters
                return;
            }

            var largeStructThreshold = Settings.GetLargeStructThresholdOrDefault(context.TryGetAnalyzerConfigOptions());
            var structSizeHelper = new LargeStructHelper(structSizeCalculator, largeStructThreshold);
            foreach (var parameterSymbol in symbol.DelegateInvokeMethod.Parameters)
            {
                WarnIfParameterIsReadOnly(structSizeHelper, parameterSymbol, diagnostic => context.ReportDiagnostic(diagnostic));
            }
        }

        private static void AnalyzeMethod(SymbolAnalysisContext context, StructSizeCalculator structSizeCalculator)
        {
            context.TryGetSemanticModel(out var semanticModel);

            var method = (IMethodSymbol) context.Symbol;
            if (IsOverridenMethod(method) || method.IsAsyncOrTaskBased(context.Compilation) || method.IsIteratorBlock())
            {
                // If the method overrides a base method or implements an interface,
                // then we can't enforce 'in'-modifier
                return;
            }

            var largeStructThreshold = Settings.GetLargeStructThresholdOrDefault(context.TryGetAnalyzerConfigOptions());
            var structSizeHelper = new LargeStructHelper(structSizeCalculator, largeStructThreshold);
            // Should analyze only subset of methods, not all of them.
            if (method.MethodKind == MethodKind.Ordinary || method.MethodKind == MethodKind.AnonymousFunction ||
                method.MethodKind == MethodKind.LambdaMethod || method.MethodKind == MethodKind.LocalFunction ||
                method.MethodKind == MethodKind.PropertyGet || method.MethodKind == MethodKind.UserDefinedOperator)
            {
                // Just using a dataflow analysis to detect that a member is captured is expensive.
                // Using a syntax-based approach first to check whether the implementation has anonymous methods.
                foreach (var p in method.Parameters)
                {
                    if (!HasAnonymousMethods(method) || !ParameterIsCapturedByAnonymousMethod(p, method, semanticModel))
                    {
                        WarnIfParameterIsReadOnly(structSizeHelper, p, diagnostic => context.ReportDiagnostic(diagnostic));
                    }
                }
            }
        }

        private static bool HasAnonymousMethods(IMethodSymbol method)
        {
            var syntax = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            if (syntax == null)
            {
                return false;
            }

            return syntax.DescendantNodes().Any(n =>
            {
                var kind = n.Kind();
                return kind == SyntaxKind.ParenthesizedLambdaExpression ||
                       kind == SyntaxKind.SimpleLambdaExpression ||
                       kind == SyntaxKind.AnonymousMethodExpression;
            });
        }

        private static bool ParameterIsCapturedByAnonymousMethod(IParameterSymbol parameter, IMethodSymbol method, SemanticModel? semanticModel)
        {
            if (semanticModel == null)
            {
                return false;
            }

            var dataFlow = method.AnalyzeDataFlow(semanticModel);
            if (dataFlow == null || !dataFlow.Succeeded)
            {
                return false;
            }

            return dataFlow.CapturedInside.FirstOrDefault(f => f.Equals(parameter, SymbolEqualityComparer.Default)) != null;
        }

        private static bool IsOverridenMethod(IMethodSymbol method)
        {
            return method.IsOverride || 
                   method.IsInterfaceImplementation();   
        }

        private static void WarnIfParameterIsReadOnly(LargeStructHelper helper, IParameterSymbol p, Action<Diagnostic> diagnosticReporter)
        {
            if (p.RefKind == RefKind.None && p.Type.IsReadOnlyStruct() && helper.IsLargeStruct(p.Type, out var estimatedSize))
            {
                Location location = p.GetParameterLocation();

                var diagnostic = Diagnostic.Create(Rule, location, p.Type.ToDisplayString(), estimatedSize);

                diagnosticReporter(diagnostic);
            }
        }
    }
}
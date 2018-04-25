using System;
using System.Collections.Immutable;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Structs
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UseInModifierForReadOnlyStructAnalyzer : DiagnosticAnalyzer
    {
        // Only suggest when the struct is greater or equals to the threashold
        public static readonly int LargeStructThreashold = 2 * sizeof(long);

        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.UseInModifierForReadOnlyStructDiagnosticId;

        private static readonly string Title = "Use in-modifier for a readonly struct";
        private static readonly string MessageFormat = "Use in-modifier for passing readonly struct '{0}'";
        private static readonly string Description = "Readonly structs have better performance when passed readonly reference";
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
            context.RegisterSyntaxNodeAction(AnalyzeDelegateDeclaration, SyntaxKind.DelegateDeclaration);
            context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
        }

        private void AnalyzeDelegateDeclaration(SyntaxNodeAnalysisContext context)
        {
            var delegateDeclaration = (DelegateDeclarationSyntax)context.Node;
            var semanticModel = context.SemanticModel;
            foreach (var p in delegateDeclaration.ParameterList.Parameters)
            {
                if (semanticModel.GetDeclaredSymbol(p) is IParameterSymbol parameterSymbol)
                {
                    WarnIfParameterIsReadOnly(context.SemanticModel, parameterSymbol, diagnostic => context.ReportDiagnostic(diagnostic));
                }
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

            // TODO: not sure how to get the semantic model another way!
            if (context.Symbol.DeclaringSyntaxReferences.Length != 0)
            {
                var syntaxTree = context.Symbol.DeclaringSyntaxReferences[0].SyntaxTree;
                var semanticModel = context.Compilation.GetSemanticModel(syntaxTree);

                // Should analyze only subset of methods, not all of them.
                // What about operators?
                if (method.MethodKind == MethodKind.Ordinary || method.MethodKind == MethodKind.AnonymousFunction ||
                    method.MethodKind == MethodKind.LambdaMethod || method.MethodKind == MethodKind.LocalFunction ||
                    method.MethodKind == MethodKind.PropertyGet || method.MethodKind == MethodKind.PropertySet)
                {
                    foreach (var p in method.Parameters)
                    {
                        WarnIfParameterIsReadOnly(semanticModel, p, diagnostic => context.ReportDiagnostic(diagnostic));
                    }
                }
            }
        }

        private static bool IsOverridenMethod(IMethodSymbol method)
        {
            return method.IsOverride || 
                   method.IsInterfaceImplementation();   
        }

        private static void WarnIfParameterIsReadOnly(SemanticModel model, IParameterSymbol p, Action<Diagnostic> diagnosticReporter)
        {
            var instanceSize = p.Type.ComputeStructSize(model);
            if (p.RefKind == RefKind.None && p.Type.IsReadOnlyStruct() && instanceSize >= LargeStructThreashold)
            {
                Location location;
                if (p.DeclaringSyntaxReferences.Length != 0)
                {
                    // Can't just use p.Location, because it will capture just a span for parameter name.
                    var span = p.DeclaringSyntaxReferences[0].GetSyntax().FullSpan;
                    location = Location.Create(p.DeclaringSyntaxReferences[0].SyntaxTree, span);
                }
                else
                {
                    location = p.Locations[0];
                }

                var diagnostic = Diagnostic.Create(Rule, location, p.Type.Name, p.Name);

                diagnosticReporter(diagnostic);
            }
        }
    }
}
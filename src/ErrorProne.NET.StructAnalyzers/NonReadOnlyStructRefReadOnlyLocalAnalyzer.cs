﻿using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.StructAnalyzers
{
    /// <summary>
    /// An analyzer that warns when non-ref-readonly struct is used as a refreadonly local variable.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NonReadOnlyStructRefReadOnlyLocalAnalyzer : DiagnosticAnalyzer
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.NonReadOnlyStructRefReadOnlyLocalDiagnosticId;

        private static readonly string Title = "Non-readonly struct used as ref readonly local variable";
        private static readonly string MessageFormat = "Non-readonly struct '{0}' used as ref readonly local '{1}'";
        private static readonly string Description = "Non-readonly structs can caused severe performance issues when used as ref readonly local";
        private const string Category = "Performance";
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

            context.RegisterSyntaxNodeAction(AnalyzeLocal, SyntaxKind.LocalDeclarationStatement);
        }

        private void AnalyzeLocal(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is LocalDeclarationStatementSyntax vs)
            {
                foreach (var local in vs.Declaration.Variables)
                {
                    if (context.SemanticModel.GetDeclaredSymbol(local) is ILocalSymbol resolvedLocal)
                    {
                        if (resolvedLocal.IsRef && resolvedLocal.RefKind == RefKind.In && resolvedLocal.Type.UnfriendlyToReadOnlyRefs())
                        {
                            // This is ref readonly variable that is not friendly for in-refs.
                            var diagnostic = Diagnostic.Create(Rule, vs.Declaration.Type.GetLocation(), resolvedLocal.Type.Name, resolvedLocal.Name);

                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Common;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.ExceptionHandlingRules
{
    /// <summary>
    /// Detects `catch` blocks that swallow an exception.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class SwallowAllExceptionAnalyzer : DiagnosticAnalyzer
    {
        // Catch is not empty, `catch` or `catch(Exception)` and some return statement exists. 
        public const string DiagnosticId = RuleIds.AllExceptionSwalled;
        internal const string Title = "Catching everything considered harmful.";
        internal const string MessageFormat = "Exit point '{0}' swallows an exception.";
        internal const string Category = "CodeSmell";

        internal static readonly DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.CatchClause);
        }

        // Called when Roslyn encounters a catch clause.
        private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
        {
            var catchBlock = context.Node as CatchClauseSyntax;

            if (catchBlock == null)
            {
                return;
            }

            if (catchBlock.Declaration == null || catchBlock.Declaration.CatchIsTooGeneric(context.SemanticModel))
            {
                var usages = context.SemanticModel.GetExceptionIdentifierUsages(catchBlock);
                if (usages.Count != 0)
                {
                    // Exception was observed!
                    return;
                }

                StatementSyntax syntax = catchBlock.Block;
                var controlFlow = context.SemanticModel.AnalyzeControlFlow(syntax);

                // Warn for every exit points
                foreach (SyntaxNode @return in controlFlow.ExitPoints)
                {
                    // Due to some very weird behavior, return statement would be an exit point of the method
                    // even if the return statement is unreachable (for instance, because throw statement is preceding it);
                    // So analyzing control flow once more and emitting a warning only when the endpoint is reachable!
                    var localFlow = context.SemanticModel.AnalyzeControlFlow(@return);

                    if (localFlow.Succeeded && localFlow.StartPointIsReachable)
                    {
                        // Block is empty, create and report diagnostic warning.
                        var diagnostic = Diagnostic.Create(Rule, @return.GetLocation(), @return.WithoutTrivia().GetText());
                        context.ReportDiagnostic(diagnostic);
                    }
                }

                // EndPoint (end of the block) is not a exit point. Should be covered separately!
                if (controlFlow.EndPointIsReachable)
                {
                    var diagnostic = Diagnostic.Create(Rule, catchBlock.Block.CloseBraceToken.GetLocation(), catchBlock.Block.CloseBraceToken.ValueText);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}

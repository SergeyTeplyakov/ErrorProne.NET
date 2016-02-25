using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Common;
using ErrorProne.NET.SideEffectRules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.FormatRules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class StringFormatCorrectnessAnalyzer : DiagnosticAnalyzer
    {
        // Diagnostic information for non-provided index
        public const string NonExistingIndexId = RuleIds.StringFormatInvalidIndexId;
        private static readonly string NonExistingIndexTitle = "Non-existing argument in the format string.";
        private static readonly string NonExistingIndexMessage = "Argument(s) {0} was not provided.";
        private static readonly string NonExistingIndexDescription = "Argument string references an argument(s) that was not provided.";
        private static readonly DiagnosticDescriptor NonExistingIndexRule = new DiagnosticDescriptor(NonExistingIndexId, NonExistingIndexTitle, NonExistingIndexMessage, Category,
            DiagnosticSeverity.Error, isEnabledByDefault: true, description: NonExistingIndexDescription);

        // Diagnostic information for excessive arguments
        public const string ExcessiveArgumentId = RuleIds.StringFormatHasEcessiveArgumentId;
        private static readonly string ExcessiveArgumentTitle = "Excessive arguments in the format string.";
        private static readonly string ExcessiveArgumentMessage = "Argument '{0}' was not used in the format string.";
        private static readonly string ExcessiveArgumentDescription = "Argument was not used in the format string.";
        private static readonly DiagnosticDescriptor ExcessiveArgumentRule = new DiagnosticDescriptor(ExcessiveArgumentId, ExcessiveArgumentTitle, ExcessiveArgumentMessage, Category,
            DiagnosticSeverity.Warning, isEnabledByDefault: true, description: ExcessiveArgumentDescription);

        // Diagnostic information for invalid format string
        public const string InvalidFormatId = RuleIds.StringFormatInvalidId;
        private static readonly string InvalidFormatTitle = "Format argument is not a valid format string.";
        private static readonly string InvalidFormatMessage = "Format argument is not a valid format string.";
        private static readonly string InvalidFormatDescription = "Format argument is not a valid format string.";
        private static readonly DiagnosticDescriptor InvalidFormatRule = new DiagnosticDescriptor(InvalidFormatId, InvalidFormatTitle, InvalidFormatMessage, Category,
            DiagnosticSeverity.Error, isEnabledByDefault: true, description: InvalidFormatDescription);

        private const string Category = "CodeSmell";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(NonExistingIndexRule, ExcessiveArgumentRule, InvalidFormatRule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeMethodInvocation, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeMethodInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            var symbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

            if (symbol != null && FormatHelper.IsFormattableCall(symbol, context.SemanticModel))
            {
                var parsedFormat = FormatHelper.ParseFormatMethodInvocation(invocation, symbol, context.SemanticModel);
                if (!parsedFormat.IsValid)
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidFormatRule, parsedFormat.FormatArgument.GetLocation()));
                }
                else
                {
                    // Special corner case: if param args is an expression that provides object or object[]
                    // then giving up the analysis!
                    int actualArgumentsLength = parsedFormat.Args.Length;

                    if (parsedFormat.Args.Length == 1)
                    {
                        ITypeSymbol expressionType = GetExpressionType(parsedFormat.Args[0], context.SemanticModel);
                        if (expressionType?.Equals(context.SemanticModel.GetClrType(typeof (object))) == true ||
                            IsArrayOfObjects(expressionType, context.SemanticModel))
                        {
                            // Giving up all the checks! Can't deal with them!
                            return;
                        }
                    }

                    // Checking for non-existed indices in a format string
                    var missedArguments = parsedFormat.UsedIndices.Where(i => i < 0 || i >= actualArgumentsLength).ToList();
                    if (missedArguments.Count != 0)
                    {
                        context.ReportDiagnostic(Diagnostic
                            .Create(
                                NonExistingIndexRule, parsedFormat.FormatArgument.GetLocation(),
                                string.Join(", ", missedArguments)));
                    }

                    // TODO: unused parameters should have warnings on the parameters themselves not on the string format!
                    var excessiveArguments =
                        Enumerable.Range(0, actualArgumentsLength)
                            .Where(i => !parsedFormat.UsedIndices.Contains(i))
                            .Select(i => parsedFormat.Args[i])
                            .ToList();

                    foreach (var excessiveArg in excessiveArguments)
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                ExcessiveArgumentRule, excessiveArg.GetLocation(), excessiveArg));
                    }
                }
            }
        }

        private bool IsArrayOfObjects(ITypeSymbol expressionType, SemanticModel semanticModel)
        {
            var arrayType = expressionType as IArrayTypeSymbol;
            return arrayType?.Rank == 1 &&
                   arrayType?.ElementType.Equals(semanticModel.GetClrType(typeof (object))) == true;
        }

        private ITypeSymbol GetExpressionType(ExpressionSyntax expression, SemanticModel semanticModel)
        {
            var expressionSymbol = semanticModel.GetSymbolInfo(expression).Symbol;
            if (expressionSymbol == null)
            {
                return null;
            }

            IMethodSymbol method = expressionSymbol as IMethodSymbol;

            if (method != null)
            {
                return method.ReturnType;
            }

            return null;
        }
    }
}
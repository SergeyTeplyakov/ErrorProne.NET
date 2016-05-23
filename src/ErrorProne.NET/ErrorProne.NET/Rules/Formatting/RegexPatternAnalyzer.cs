using System;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using ErrorProne.NET.Common;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SyntaxKind = Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace ErrorProne.NET.Rules.Formatting
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RegexPatternAnalyzer : DiagnosticAnalyzer
    {
        // Diagnostic information for non-provided index
        public const string NonExistingIndexId = RuleIds.RegexPatternIsInvalid;
        private static readonly string NonExistingIndexTitle = "Regex pattern is invalid.";
        private static readonly string NonExistingIndexMessage = "Regex pattern is invalid: '{0}'.";
        private static readonly string NonExistingIndexDescription = "Provided regex is invalid.";
        private static readonly DiagnosticDescriptor InvalidRegexRule = new DiagnosticDescriptor(NonExistingIndexId, NonExistingIndexTitle, NonExistingIndexMessage, Category,
            DiagnosticSeverity.Error, isEnabledByDefault: true, description: NonExistingIndexDescription);

        // Will give up if the analysis will take longer than that!
        public static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);

        private const string Category = "CodeSmell";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(InvalidRegexRule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeRegexCreation, SyntaxKind.ObjectCreationExpression);
        }

        private void AnalyzeRegexCreation(SyntaxNodeAnalysisContext context)
        {
            var objectCreation = (ObjectCreationExpressionSyntax) context.Node;
            var type = context.GetTypeSymbol(objectCreation.Type);
            if (type == null || !type.Equals(context.SemanticModel.GetClrType(typeof(Regex))))
            {
                return;
            }

            var pattern = objectCreation.ArgumentList.Arguments[0].Expression.GetLiteral(context.SemanticModel);
            if (pattern == null)
            {
                return;
            }

            try
            {
                var regex = new Regex(pattern, RegexOptions.None, Timeout);
            }
            catch (ArgumentException e)
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidRegexRule, objectCreation.ArgumentList.Arguments[0].GetLocation(), e.Message));
            }
        }
    }
}
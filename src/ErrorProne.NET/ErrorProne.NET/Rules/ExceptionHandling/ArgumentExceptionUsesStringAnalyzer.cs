using System;
using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Common;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Rules.ExceptionHandling {
    /// <summary>
    /// Detects `ArgumentException` ctor's that use strings instead of nameof operator for argument names
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ArgumentExceptionUsesStringAnalyzer : DiagnosticAnalyzer {
        private const string ParamNameRequiredTitle = "ArgumentException should provide argument name.";
        private const string ParamNameRequiredMessage = "ArgumentException instance misses invalid argument name.";
        private const string ParamNameRequiredDescription = "ArgumentException indicates that a method was called with an invalid argument, so it is better to provide argument name for further investigation of exception cause.";

        private const string MethodHasNoSuchParamNameRuleTitle = "Declaring method has no argument with specified name.";
        private const string MethodHasNoSuchParamNameRuleMessageFormat = "{0} '{1}{2}' has no argument with name '{3}'.";
        private const string MethodHasNoSuchParamNameRuleDescription = "ArgumentException indicates that a method on the top of the call stack was called with an invalid argument, so it is confusing if ArgumentException.ParamName provides missing paramater name. Consider using nameof operator to avoid typos and post-refactoring errors.";

        private const string ParamNameShouldNotBeStringRuleTitle = "Use nameof instead of string.";
        private const string ParamNameShouldNotBeStringRuleMessageFormat = "ArgumentException constructor obtains parameter name from string \"{0}\" instead of nameof({0}).";
        private const string ParamNameShouldNotBeStringRuleDescription = "It is better to obtain string name of the paramater by using nameof operator to avoid typos and post-refactoring errors.";

        private const string Category = "CodeSmell";

        private static readonly DiagnosticDescriptor ParamNameRequiredRule =
            new DiagnosticDescriptor(RuleIds.ArgumentExceptionParamNameRequired, ParamNameRequiredTitle, ParamNameRequiredMessage, Category, DiagnosticSeverity.Warning, description: ParamNameRequiredDescription, isEnabledByDefault: true);
        private static readonly DiagnosticDescriptor MethodHasNoSuchParamNameRule =
            new DiagnosticDescriptor(RuleIds.ArgumentExceptionMethodHasNoSuchParamName, MethodHasNoSuchParamNameRuleTitle, MethodHasNoSuchParamNameRuleMessageFormat, Category, DiagnosticSeverity.Warning, description: MethodHasNoSuchParamNameRuleDescription, isEnabledByDefault: true);
        private static readonly DiagnosticDescriptor ParamNameShouldNotBeStringRule =
            new DiagnosticDescriptor(RuleIds.ArgumentExceptionParamNameShouldNotBeString, ParamNameShouldNotBeStringRuleTitle, ParamNameShouldNotBeStringRuleMessageFormat, Category, DiagnosticSeverity.Warning, description: ParamNameShouldNotBeStringRuleDescription, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(ParamNameRequiredRule, MethodHasNoSuchParamNameRule, ParamNameShouldNotBeStringRule);

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        }

        private void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context) {
            var objectCreation = (ObjectCreationExpressionSyntax)context.Node;
            var method = context.GetCtorSymbol(objectCreation);
            if (!Equals(method.ContainingType, context.GetClrType<ArgumentException>())) {
                return;
            }

            var stringType = context.GetClrType<string>();
            var paramNameArgument = method.Parameters.FirstOrDefault(p => p.Name == "paramName");
            if (paramNameArgument == null || !Equals(paramNameArgument.Type, stringType)) {
                context.ReportDiagnostic(Diagnostic.Create(ParamNameRequiredRule, context.Node.GetLocation()));
                return;
            }

            var argumentIndex = method.Parameters.IndexOf(paramNameArgument);
            var argument = objectCreation.ArgumentList.Arguments[argumentIndex];
            if (argument.Expression is LiteralExpressionSyntax) {
                var literal = argument.Expression.ToString();
                literal = literal.Substring(1, literal.Length - 2);

                var methodBaseDeclaration = context.Node.EnumerateParents()
                    .OfType<BaseMethodDeclarationSyntax>()
                    .FirstOrDefault(d => d is MethodDeclarationSyntax || d is ConstructorDeclarationSyntax);

                if (methodBaseDeclaration == null) {
                    return;
                }

                var ctorDeclaration = methodBaseDeclaration as ConstructorDeclarationSyntax;

                var parameters = methodBaseDeclaration.ParameterList.Parameters;
                if (parameters.Any(p => p.Identifier.ValueText.Equals(literal))) {
                    context.ReportDiagnostic(Diagnostic.Create(ParamNameShouldNotBeStringRule, argument.GetLocation(), literal));
                } else if (ctorDeclaration != null) {
                    context.ReportDiagnostic(Diagnostic.Create(MethodHasNoSuchParamNameRule, argument.GetLocation(), 
                        "Constructor", ctorDeclaration.Identifier.Text, ctorDeclaration.ParameterList.ToString(), literal));
                } else {
                    var methodDeclaration = (MethodDeclarationSyntax)methodBaseDeclaration;
                    context.ReportDiagnostic(Diagnostic.Create(MethodHasNoSuchParamNameRule, argument.GetLocation(),
                        "Method", methodDeclaration.Identifier.Text, methodDeclaration.ParameterList.ToString(), literal));
                }
            }
        }
    }
}

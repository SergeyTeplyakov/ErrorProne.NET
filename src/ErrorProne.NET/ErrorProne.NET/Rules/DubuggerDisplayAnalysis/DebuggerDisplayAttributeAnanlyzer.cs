using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using ErrorProne.NET.Common;
using ErrorProne.NET.Extensions;
using ErrorProne.NET.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace ErrorProne.NET.Rules.ExceptionHandling
{
    /// <summary>
    /// Analyzer that ensures that <see cref="DebuggerDisplayAttribute"/> has correct format values.
    /// </summary>
    /// <remarks>
    /// Here some requirements that I've come up for DebuggerDisplayAttribute:
    /// - Value has following format: "text: {expression[,nq]}"
    /// - expression could be:
    ///   * any valid C# expression
    ///   * could reference instance/static field/property/method
    ///   * referenced method should return non-void (if the result is not a string, then
    ///     ToString would be called on it).
    ///   * method could take arguments they should be correct
    ///   * method could have default arguments than just a method invocation is possible, like <code>foo()</code>
    ///     when foo has argument like <code>string foo(int n = 42)</code>.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DebuggerDisplayAttributeAnanlyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = RuleIds.DebuggerDisplayAttributeInvalidFormat;

        internal const string Title = "DebuggerDisplayAttribute has invalid value";
        internal const string MessageFormat = "{0}";
        internal const string Category = "CodeSmell";

        /// <summary>
        /// Set of known specifiers that user can provide for the expression.
        /// Currently the only known one is 'nq' which stands for 'no quotes'.
        /// </summary>
        private static HashSet<string> _knownSpecifiers = new HashSet<string>(new [] {"nq"});

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
        }

        private void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
        {
            var attributeSyntax = (AttributeSyntax)context.Node;

            var semanticAttribute = context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol;
            if (semanticAttribute == null || !semanticAttribute.ContainingType.IsType(typeof(DebuggerDisplayAttribute)))
            {
                return;
            }

            // DebuggerDisplayAttribute has at least one required argument
            var firstArgument = attributeSyntax.ArgumentList.Arguments.First();
            var literal = GetLiteral(firstArgument, context.SemanticModel);

            // First checking that number of braces is correct.
            // This degrades perf a bit, but simplifies implementation.
            var braceMatch = CheckBraces(literal);
            if (braceMatch != UnmachedBraces.Match)
            {
                var message = braceMatch == UnmachedBraces.UnmachedCloseBrace
                    ? "Can't find corresponding open brace '{' in the input string."
                    : "Can't find corresponding close brace '}' in the input string.";
                context.ReportDiagnostic(Diagnostic.Create(Rule, firstArgument.GetLocation(), message));
                return;
            }

            // Need to find for what class this attribute was applied
            var classDeclaration = attributeSyntax.EnumerateParents().OfType<ClassDeclarationSyntax>().First();
            var classDefinition = context.SemanticModel.GetDeclaredSymbol(classDeclaration);

            var errors = 
                ExtractExpressions(literal)
                .Select(e => ParseExpression(e, classDefinition, context.SemanticModel))
                .Where(e => !e.IsValid)
                .ToList();

            if (errors.Count != 0)
            {
                string message = errors.Count == 1
                    ? $"Expression '{errors[0].Expression}' is invalid: {errors[0].ErrorMessage}"
                    : $"Expressions {string.Join(", ", errors.Select(s => $"'{s}'"))} are invalid: {string.Join(", ", errors.Select(s => s.ErrorMessage))}";
                context.ReportDiagnostic(Diagnostic.Create(Rule, firstArgument.GetLocation(), message));
            }
        }

        internal class ParsedExpression
        {
            private readonly string _errorMessage;

            private ParsedExpression(string expression, string errorMessage, bool isValid)
            {
                Expression = expression;
                _errorMessage = errorMessage;
                IsValid = isValid;
            }

            public static ParsedExpression Valid(string expression)
            {
                return new ParsedExpression(expression, errorMessage: null, isValid: true);
            }

            public static ParsedExpression Invalid(string expression, string errorMessage)
            {
                return new ParsedExpression(expression: expression, errorMessage: errorMessage, isValid: false);
            }

            public bool IsValid { get; }
            public string Expression { get; }

            public string ErrorMessage
            {
                get
                {
                    Contract.Requires(!IsValid);
                    return _errorMessage;
                }
            }
        }

        private ParsedExpression ParseExpression(string expression, ISymbol classDefinition, SemanticModel semanticModel)
        {
            // Expression in DebuggerDisplayAttribute could have special qualifier, like: {ToString(), nq}
            var pieces = expression.Split(new [] {","}, StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Length > 2)
            {
                return ParsedExpression.Invalid(expression, $"Expression should have only one specifier but got {pieces.Length - 1}");
            }

            if (pieces.Length == 2 && !_knownSpecifiers.Contains(pieces[1].Trim()))
            {
                string additionalMessage = 
                    _knownSpecifiers.Count > 1 
                    ? $"Known specifiers are '{string.Join(", ", _knownSpecifiers.Select(s => $"'{s}'"))}'"
                    : $"Known specifier is '{_knownSpecifiers.First()}'";

                return ParsedExpression.Invalid(expression, 
                    $"Unknown expression specifier '{pieces[1].Trim()}'. {additionalMessage}");
            }

            // Ok, it seems that expression is valid, now need to check that it points to valid field/property/method
            var originalClassDefinition = (ClassDeclarationSyntax)classDefinition.DeclaringSyntaxReferences.First().GetSyntax();
            const string magicMethodName = "SomeMethodThatDefinitelyDoesNotExistsInOriginalClass";
            
            // Expressions in the attribute is very very similar to string interpolation.
            // To validate it let's create a code snippet that will use original expression
            // but in the interpolated string.
            // I.e. for expression like `DebuggerDisplay("{x}")` we'll generate:
            // var x = $"{x}";
            const string expressionToParseTemplate =
@"private void {0}() {{
    var x = $""{{{1}}}"";
  }}";
            var expressionToParse = string.Format(expressionToParseTemplate, magicMethodName, pieces[0]);
            var tree = CSharpSyntaxTree.ParseText(expressionToParse);

            // First, need to check that expression is valid.
            var compiledUnit = (CompilationUnitSyntax)tree.GetRoot();
            if (compiledUnit.ContainsDiagnostics)
            {
                string message = string.Join(", ", compiledUnit.GetDiagnostics().Select(d => $"'{d.GetMessage()}'"));
                return ParsedExpression.Invalid(expression, message);
            }

            // Let's add new method to the class and try to compile it.
            var parsedMember = compiledUnit.Members[0];
            var modifiedClassDefinition = originalClassDefinition.AddMembers(parsedMember);

            // Now we need to replace old syntax tree with new one to check that new one is correct
            var oldSyntaxTree = semanticModel.SyntaxTree;
            var compilationUnit = (CompilationUnitSyntax) oldSyntaxTree.GetRoot();
            compilationUnit = compilationUnit.WithMembers(new SyntaxList<MemberDeclarationSyntax>().Add(modifiedClassDefinition));

            var newSyntaxTree = compilationUnit.SyntaxTree;
            
            // Getting compilation
            var compilation = semanticModel.Compilation.ReplaceSyntaxTree(oldSyntaxTree, newSyntaxTree);
            
            // Compilation could fail, but we need to check that error is actually happening
            // inside our generated method (because code could be broken and could have errors).
            var relevantFailures = new List<Diagnostic>();
            foreach (var diag in compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
            {
                // Need to check that diagnostic was emitted in the generated method
                var node = diag.Location.SourceTree?.GetRoot()?.FindNode(diag.Location.SourceSpan);
                if (node == null) continue;

                var method = node.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                if (method?.Identifier.Text == magicMethodName)
                {
                    relevantFailures.Add(diag);
                }
            }

            if (relevantFailures.Count > 0)
            {
                string message = string.Join(", ", relevantFailures.Select(d => $"'{d.GetMessage()}'"));
                return ParsedExpression.Invalid(expression, message);
            }
            
            return ParsedExpression.Valid(expression);
        }

        private string GetLiteral(AttributeArgumentSyntax argument, SemanticModel semanticModel)
        {
            var literal = argument.Expression.GetLiteral(semanticModel);
            // Literal would be wrapped in double quotes, removing them
            return literal.Substring(1, literal.Length - 2);
        }

        enum UnmachedBraces
        {
            Match,
            UnmachedOpenBrace,
            UnmachedCloseBrace,
        }

        private UnmachedBraces CheckBraces(string literal)
        {
            int currentBraceCount = 0;
            foreach (var c in literal)
            {
                if (c == '{') currentBraceCount++;
                else if (c == '}') currentBraceCount--;

                if (currentBraceCount < 0 )
                    return UnmachedBraces.UnmachedCloseBrace;
                // Can fail immediately when string has two closing braces in a row
                if (currentBraceCount > 1)
                    return UnmachedBraces.UnmachedOpenBrace;
            }

            return currentBraceCount > 0 ? UnmachedBraces.UnmachedOpenBrace : UnmachedBraces.Match;
        }

        private List<string> ExtractExpressions(string literal)
        {
            List<string> expressions = new List<string>();
            int position = 0;
            while (position < literal.Length)
            {
                position = literal.IndexOf('{', position);
                if (position == -1) break;

                var endPosition = literal.IndexOf('}', position);
                if (endPosition == -1) break;

                // Need to skip '{' and '}'
                var expression = literal.Substring(position + 1, endPosition - position - 1);
                expressions.Add(expression);

                position = endPosition;
            }

            return expressions;
        }
    }
}

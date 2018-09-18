using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace SwitchAnalyzer
{
    public class ClassAnalyzer
    {
        private const string Category = "Correctness";
        public const string DiagnosticId = "EPW03";
        private const string Title = "Non exhaustive patterns in switch block";
        private const string MessageFormat = "Switch case should check implementation of type(s): {0}";
        private const string Description = "All class implementations in pattern matching switch statement should be checked.";
        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticId, 
            title: Title, 
            messageFormat: MessageFormat, 
            category: Category, 
            defaultSeverity: DiagnosticSeverity.Warning, 
            isEnabledByDefault: true, 
            description: Description);

        public static bool ShouldProceedWithChecks(SyntaxList<SwitchSectionSyntax> caseSyntaxes, string expressionTypeName)
        {
            if (PatternMatchingHelper.HasVarDeclaration(caseSyntaxes))
            {
                return false;
            }

            if (expressionTypeName == "Object")
            {
                // todo: maybe show warning or info message in this case?
                return false;
            }

            if (HasSameClassDeclaration(caseSyntaxes, expressionTypeName))
            {
                return false;
            }

            return DefaultCaseCheck.ShouldProceedWithDefault(caseSyntaxes);
        }

        private static bool HasSameClassDeclaration(SyntaxList<SwitchSectionSyntax> caseSyntaxes, string className)
        {
            return PatternMatchingHelper.GetCaseValues(caseSyntaxes).Any(x => x == className); 
        }

        public static IEnumerable<SwitchArgumentTypeItem<string>> GetAllImplementationNames(
            int switchStatementLocationStart,
            ITypeSymbol className,
            SemanticModel semanticModel)
        {
            var allSymbols = semanticModel.LookupSymbols(switchStatementLocationStart);
            var namedTypeSymbols = allSymbols.Where(x => x.Kind == SymbolKind.NamedType).OfType<INamedTypeSymbol>();
            var implementations = namedTypeSymbols
                .Where(namedType => namedType.BaseType?.Name == className.Name
                                    && namedType.ContainingNamespace.Name == className.ContainingNamespace.Name
                                    && !namedType.IsAbstract);
            // todo: Decide what to do with inheritors of abstract class that is inheritor of base class.
            return implementations.Select(x => new SwitchArgumentTypeItem<string>(
                prefix: x.ContainingNamespace.Name,
                member: x.Name,
                fullName: x.Name,
                value:x.Name));
        }
    }
}

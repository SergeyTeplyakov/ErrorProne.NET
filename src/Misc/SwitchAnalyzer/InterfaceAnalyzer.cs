using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SwitchAnalyzer
{
    public class InterfaceAnalyzer
    {
        private const string Category = "Correctness";
        public const string DiagnosticId = "SA002";
        private const string Title = "Non exhaustive patterns in switch block";
        private const string MessageFormat = "Switch case should check interface implementation of type(s): {0}";
        private const string Description = "All interface implementations in pattern matching switch statement should be checked.";
        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticId,
            title: Title,
            messageFormat: MessageFormat, 
            category: Category, 
            defaultSeverity: DiagnosticSeverity.Warning, 
            isEnabledByDefault: true, 
            description: Description);


        public static bool ShouldProceedWithChecks(SyntaxList<SwitchSectionSyntax> caseSyntaxes)
        {
            if (PatternMatchingHelper.HasVarDeclaration(caseSyntaxes))
            {
                return false;
            }

            return DefaultCaseCheck.ShouldProceedWithDefault(caseSyntaxes);
        }

        public static IEnumerable<SwitchArgumentTypeItem<string>> GetAllImplementationNames(
            int switchStatementLocationStart,
            ITypeSymbol interfaceType,
            SemanticModel semanticModel)
        {
            var allSymbols = semanticModel.LookupSymbols(switchStatementLocationStart);           
            var namedTypeSymbols = allSymbols.Where(x => x.Kind == SymbolKind.NamedType).OfType<INamedTypeSymbol>();
            var implementations = namedTypeSymbols.Where(namedType => namedType.Interfaces.Any(x =>
                x.Name == interfaceType.Name
                && x.ContainingNamespace.Name == interfaceType.ContainingNamespace.Name));
            return implementations.Select(x => new SwitchArgumentTypeItem<string>(
                prefix: x.ContainingNamespace.Name,
                member: x.Name,
                fullName: x.Name,
                value:x.Name));
        }
    }
}

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ErrorProne.NET.Extensions;
using ErrorProne.NET.StructAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.Net.StructAnalyzers.NonDefaultStructs
{
    /// <summary>
    /// Analyzer warns when a struct with non-default invariants is embedded into another struct.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NonDefaultableStructsFieldAnalyzer : NonDefaultableStructAnalyzerBase
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.DoNotEmbedStructsMarkedWithDoUseDefaultConstructionForStruct;

        private const string Title = "Do not embed non-defaultable structs into another structs.";

        private const string Message =
            "Do not embed a non-defaultable struct '{0}' into another struct.";
        private const string Description = "Non-defaultable structs should be " +
                                                     "constructed using a non-default constructor and can not be embedded " +
                                                     "in other defaultable structs .";
        
        private const string Category = "CodeSmell";

        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        private static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, Message, Category, Severity, isEnabledByDefault: true, description: Description);

        /// <nodoc />
        public NonDefaultableStructsFieldAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);
        }

        private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
        {
            var propertyDeclaration = (PropertyDeclarationSyntax) context.Node;
            var type = context.SemanticModel.GetTypeInfo(propertyDeclaration.Type);
            
            var containingType = GetContainingType(propertyDeclaration, context.SemanticModel);
            if (!ShouldBeInspected(containingType))
            {
                return;
            }
            
            // It is ok to embed one struct marked with special attribute into another struct
            // marked with the same special attribute.
            
            if (propertyDeclaration.IsAutoProperty() && !HasDoNotUseDefaultConstructionOrSpecial(context.Compilation, containingType, out _))
            {
                ReportDiagnosticForTypeIfNeeded(context.Compilation, propertyDeclaration, type.Type, Rule,
                    context.ReportDiagnostic);
            }
        }

        private void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context)
        {
            var fieldDeclaration = (FieldDeclarationSyntax) context.Node;
            var type = context.SemanticModel.GetTypeInfo(fieldDeclaration.Declaration.Type);

            var containingType = GetContainingType(fieldDeclaration, context.SemanticModel);
            if (!ShouldBeInspected(containingType))
            {
                return;
            }
            
            // It is ok to embed one struct marked with special attribute into another struct
            // marked with the same special attribute.
            if (!HasDoNotUseDefaultConstructionOrSpecial(context.Compilation, containingType, out _))
            {
                ReportDiagnosticForTypeIfNeeded(context.Compilation, fieldDeclaration, type.Type, Rule, context.ReportDiagnostic);
            }
        }

        private static bool ShouldBeInspected([NotNullWhen(true)] ITypeSymbol? typeSymbol) =>
            typeSymbol != null && typeSymbol.TypeKind == TypeKind.Struct;

        private static ITypeSymbol? GetContainingType(PropertyDeclarationSyntax property, SemanticModel model)
        {
            var propertySymbol = model.GetDeclaredSymbol(property);
            return propertySymbol?.ContainingType;
        }
        
        private static ITypeSymbol? GetContainingType(FieldDeclarationSyntax field, SemanticModel model)
        {
            var fieldSymbol = field.Declaration.Variables.Select(v => model.GetDeclaredSymbol(v)).FirstOrDefault();
            return fieldSymbol?.ContainingType;
        }
    }
}
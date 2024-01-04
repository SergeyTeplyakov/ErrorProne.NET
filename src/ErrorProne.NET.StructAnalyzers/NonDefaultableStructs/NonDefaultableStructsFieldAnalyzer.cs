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
        public static string DiagnosticId => Rule.Id;

        /// <nodoc />
        private static DiagnosticDescriptor Rule => DiagnosticDescriptors.EPS11;

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
            if (containingType is null || IsStructLike(containingType))
            {
                return;
            }
            
            // It is ok to embed one struct marked with special attribute into another struct
            // marked with the same special attribute.
            
            if (propertyDeclaration.IsAutoProperty() && !containingType.DoNotUseDefaultConstruction(context.Compilation, out _))
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
            if (containingType is null || IsStructLike(containingType))
            {
                return;
            }
            
            // It is ok to embed one struct marked with special attribute into another struct
            // marked with the same special attribute.
            if (!containingType.DoNotUseDefaultConstruction(context.Compilation, out _))
            {
                ReportDiagnosticForTypeIfNeeded(context.Compilation, fieldDeclaration, type.Type, Rule, context.ReportDiagnostic);
            }
        }

        private static bool IsStructLike(ITypeSymbol typeSymbol)
        {
            // Extracting logic because it can became more complicated in the future (for instance, with struct records).
            return typeSymbol.IsValueType;
        }

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
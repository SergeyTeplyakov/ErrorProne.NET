using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Core;
using ErrorProne.NET.CoreAnalyzers;
using ErrorProne.NET.StructAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.Net.StructAnalyzers.NonDefaultStructs
{
    /// <summary>
    /// An analyzer that checks that a struct marked with <see cref="NonDefaultableStructAnalysis.NonDefaultableAttributeName"/> is declared correctly
    /// (for instance, that it does have a non-default constructor).
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NonDefaultableStructDeclarationAnalyzer : DiagnosticAnalyzerBase
    {
        private static readonly DiagnosticDescriptor DiagnosticDescriptor = DiagnosticDescriptors.EPS13;

        /// <nodoc />
        public NonDefaultableStructDeclarationAnalyzer() : base(DiagnosticDescriptor)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeStructDeclaration, SyntaxKind.StructDeclaration);
        }

        private void AnalyzeStructDeclaration(SyntaxNodeAnalysisContext context)
        {
            var structDeclaration = (StructDeclarationSyntax)context.Node;
            var semanticMode = context.SemanticModel;

            var typeSymbol = semanticMode.GetDeclaredSymbol(structDeclaration); 
            if (typeSymbol is not null &&
                typeSymbol.DoNotUseDefaultConstruction(context.Compilation, out _))
            {
                // All the structs have a default constructor, but explicit constructors
                // have non empty 'DeclaringSyntaxReferences'.
                if (!typeSymbol.InstanceConstructors.Any(c => c.DeclaringSyntaxReferences.Length != 0))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptor, structDeclaration.Identifier.GetLocation(), structDeclaration.Identifier.ToFullString()));
                }
            }
        }
    }
}
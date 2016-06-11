using System;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using ErrorProne.NET.Annotations;
using ErrorProne.NET.Common;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Rules.OtherRules
{
    /// <summary>
    /// Analyzer that warns on invlid usage of <see cref="ReadOnlyAttribute"/>.
    /// </summary>
    /// <remarks>
    /// It is make no sense to use <see cref="ReadOnlyAttribute"/> on small structs or on structs
    /// with built-in types.
    /// This analyzer will warn in those cases.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DoNotUseReadOnlyAttributeAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Threshold when the rule would be applied.
        /// </summary>
        public static readonly int ThresholdSize = IntPtr.Size * 8;

        private static readonly string Title = "Do not use readonly modifier on the large struct.";
        private static readonly string Message = "Do not use readonly modifier on the large struct ({0} bytes).";
        private static readonly string Description = "Readonly modifier on structs lead to additional copies that could be harmful for performance.";

        private const string Category = "CodeSmell";

        private static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(RuleIds.ReadonlyAttributeNotOnCustomStructs, Title, Message, Category,
                DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
        }

        private void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context)
        {
            var fieldDeclarations = (FieldDeclarationSyntax)context.Node;
            foreach (var fieldDeclaration in fieldDeclarations.Declaration.Variables)
            {
                var fieldSymbol = context.SemanticModel.GetDeclaredSymbol(fieldDeclaration) as IFieldSymbol;

                Contract.Assert(fieldSymbol != null);

                // Looking for a field with ReadOnlyAttribute
                if (!fieldSymbol.HasReadOnlyAttribute()) continue;

                // Warn if the field is readonly and reference type or primitive.
                if (fieldSymbol.IsReadOnly || 
                    fieldSymbol.Type.IsReferenceType ||
                    fieldSymbol.Type.IsEnum() || fieldSymbol.Type.IsNullableEnum(context.SemanticModel) ||
                    fieldSymbol.Type.IsPrimitiveType() || fieldSymbol.Type.IsNullablePrimitiveType(context.SemanticModel))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(Rule, fieldDeclaration.GetLocation()));
                }
            }
        }
    }
}
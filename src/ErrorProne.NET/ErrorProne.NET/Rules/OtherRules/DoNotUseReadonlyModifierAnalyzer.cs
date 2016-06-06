using System;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using ErrorProne.NET.Common;
using ErrorProne.NET.Extensions;
using ErrorProne.NET.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Rules.OtherRules
{
    /// <summary>
    /// Analyzer warns on a large structs being used as readonly fields.
    /// </summary>
    /// <remarks>
    /// This analyzer is dubious but could be theoretically useful in some cases.
    /// For all readonly fields C# compiler emits a copy instruction on each access.
    /// For large structs (with sizes like 40-60 bytes) this behaviour could cause
    /// reasonable perf degradation.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DoNotUseReadonlyModifierAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Threshold when the rule would be applied.
        /// </summary>
        public static readonly int ThresholdSize = IntPtr.Size * 6;

        private static readonly string Title = "Do not use readonly modifier on the large struct.";
        private static readonly string Message = "Do not use readonly modifier on the large struct ({0} bytes).";
        private static readonly string Description = "Readonly modifier on structs lead to additional copies that could be harmful for performance.";

        private const string Category = "Performance";

        public static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(RuleIds.UseReadOnlyAttributeInstead, Title, Message, Category,
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

                if (fieldSymbol.HasReadOnlyAttribute()) continue;

                if (fieldSymbol.IsReadOnly && 
                    fieldSymbol.Type.IsValueType && 
                    fieldSymbol.Type.ComputeStructSize(context.SemanticModel) > ThresholdSize)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, fieldDeclarations.GetLocation()));
                }
            }
        }
    }
}
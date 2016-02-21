using System.Collections;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using ErrorProne.NET.Common;
using ErrorProne.NET.Core;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.SideEffectRules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AssignmentFreeImmutableObjectConstructionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = RuleIds.AssignmentFreeImmutableObjectContructionId;

        private static readonly string Title = "Assignment-free immutable object construction.";
        private static readonly string Message = "Assignment-free immutable object construction.";
        private static readonly string Description = "Newly created immutable object was not stored in any form!";

        // This is just a warning! Maybe an exception via attributes or something should be added!
        // TODO: add attribute that will suppress this warning!
        private const string Category = "CodeSmell";

        // Disabing this rule, because it leads to tons of false positives
        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, Message, Category,
            DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        }

        private void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
        {
            var objectCreation = (ObjectCreationExpressionSyntax) context.Node;

            var symbol = context.SemanticModel.GetSymbolInfo(objectCreation.Type).Symbol as ITypeSymbol;

            if (symbol != null && objectCreation.Parent is ExpressionStatementSyntax && 
                !symbol.IsExceptionType(context.SemanticModel))
            {
                // Can warn on immutable types and default ctors on structs!
                if (symbol.IsImmutable(context.SemanticModel) 
                    // Enum creation would be covered as well!
                    || IsDefaultCtorOnStruct(objectCreation, symbol, context.SemanticModel)
                    || IsCollection(symbol, context.SemanticModel))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation()));
                }
            }
        }

        private bool IsCollection(ITypeSymbol symbol, SemanticModel semanticModel)
        {
            return symbol.AllInterfaces.Any(i => i.Equals(semanticModel.GetClrType(typeof(IEnumerable))));
        }

        private bool IsDefaultCtorOnStruct(ObjectCreationExpressionSyntax objectCreation, ITypeSymbol symbol, SemanticModel semanticModel)
        {
            if (symbol.IsValueType)
            {
                var ctor = semanticModel.GetSymbolInfo(objectCreation).Symbol as IMethodSymbol;
                if (ctor != null)
                {
                    return ctor.Parameters.Length == 0;
                }
            }

            return false;
        }
    }
}
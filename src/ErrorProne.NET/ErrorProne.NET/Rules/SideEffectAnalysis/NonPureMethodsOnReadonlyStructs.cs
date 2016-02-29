using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using ErrorProne.NET.Common;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Rules.SideEffectAnalysis
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NonPureMethodsOnReadonlyStructs : DiagnosticAnalyzer
    {
        public const string DiagnosticId = RuleIds.NonPureMethodsOnReadonlyStructs;

        private static readonly string Title = "Non-pure method call detected on readonly struct.";
        private static readonly string Message = "Non-pure method call '{0}' detected on readonly struct.";
        private static readonly string Description = "Non-pure method call detected on readonly struct.";

        private const string Category = "CodeSmell";

        // Disabing this rule, because it leads to tons of false positives
        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, Message, Category,
            DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeInvocatonExpression, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeInvocatonExpression(SyntaxNodeAnalysisContext context)
        {
            var invocationExpression = (InvocationExpressionSyntax) context.Node;
            MemberAccessExpressionSyntax memberAccess = GetLastMemberAccessOrNull(invocationExpression);
            if (memberAccess == null) return;

            // For different type of expression field is located in different parts of the expression.
            // for a.b.c.d field is memberAccess.Name, but for a.b - is memberAccess.Expression
            ExpressionSyntax fieldAccessExpression = 
                memberAccess == invocationExpression.Expression
                ? memberAccess.Expression
                : memberAccess.Name;

            var memberAccessSymbol = context.SemanticModel.GetSymbolInfo(fieldAccessExpression).Symbol as IFieldSymbol;
            // Interested only in readonly fields
            if (memberAccessSymbol == null || !memberAccessSymbol.IsReadOnly) return;

            var methodSymbol = context.SemanticModel.GetSymbolInfo(invocationExpression).Symbol as IMethodSymbol;
            // Interested only in structs!
            if (methodSymbol == null || !methodSymbol.ContainingType.IsValueType) return;

            var pureVerifier = new PureMethodVerifier(context.SemanticModel);
            if (!pureVerifier.IsPure(methodSymbol) && IsStructMutable(methodSymbol.ReceiverType))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocationExpression.GetNodeLocationForDiagnostic(), methodSymbol.Name));
            }
        }

        private bool IsStructMutable(ITypeSymbol type)
        {
            Contract.Requires(type.IsValueType);

            var members = type.GetMembers().OfType<IFieldSymbol>().ToList();
            
            // If all members are immutable, then the struct could be considered as shallow immutable
            if (members.All(m => (m.IsReadOnly || m.IsStatic || m.IsConst)))
            {
                return false;
            }

            // Potentially additional logic could be added to consider as immutable even types that
            // has non-readonly fields that are not changed!
            return true;
        }

        private MemberAccessExpressionSyntax GetLastMemberAccessOrNull(InvocationExpressionSyntax syntax)
        {
            MemberAccessExpressionSyntax memberAccess = syntax.Expression as MemberAccessExpressionSyntax;
            while (true)
            {
                if (memberAccess == null)
                {
                    return null;
                }
                var child = memberAccess.Expression as MemberAccessExpressionSyntax;
                if (child == null)
                {
                    return memberAccess;
                }

                memberAccess = child;
            } 
        }
    }
}
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// Analyzer that warns when the return value of a method marked with MustUseResultAttribute is not used.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MustUseResultAnalyzer : UnobservedResultAnalyzerBase
    {
        private static DiagnosticDescriptor Rule => DiagnosticDescriptors.EPC34;

        /// <nodoc />
        public MustUseResultAnalyzer()
            : base(Rule)
        {
        }

        protected override bool ShouldAnalyzeMethod(IMethodSymbol method, Compilation compilation)
        {
            return HasMustUseResultAttribute(method, compilation);
        }

        protected override Diagnostic CreateDiagnostic(IInvocationOperation invocation)
        {
            var location = GetLocationForDiagnostic(invocation);
            return Diagnostic.Create(Rule, location, invocation.TargetMethod.Name);
        }

        protected override Diagnostic CreateAwaitDiagnostic(IAwaitOperation awaitOperation)
        {
            // For await expressions, we can't easily determine the method name, so we use the type name
            return Diagnostic.Create(Rule, awaitOperation.Syntax.GetLocation(), awaitOperation.Type!.Name);
        }

        private static Location GetLocationForDiagnostic(IInvocationOperation invocation)
        {
            // Try to get the best location for the diagnostic
            var syntax = invocation.Syntax;
            if (syntax is Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax invocationSyntax)
            {
                var simpleMemberAccess = invocationSyntax.Expression as Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax;
                return (simpleMemberAccess?.Name ?? invocationSyntax.Expression).GetLocation();
            }
            
            return syntax.GetLocation();
        }

        private static bool HasMustUseResultAttribute(IMethodSymbol method, Compilation compilation)
        {
            // Check if the method itself has the attribute
            if (HasMustUseResultAttributeCore(method, compilation))
            {
                return true;
            }

            // Check overridden methods
            var current = method.OverriddenMethod;
            while (current != null)
            {
                if (HasMustUseResultAttributeCore(current, compilation))
                {
                    return true;
                }
                current = current.OverriddenMethod;
            }

            // Check implemented interface methods
            var interfaceImplementations = method.ContainingType.AllInterfaces
                .SelectMany(i => i.GetMembers().OfType<IMethodSymbol>())
                .Where(interfaceMethod =>
                    method.ContainingType.FindImplementationForInterfaceMember(interfaceMethod)?.Equals(method, SymbolEqualityComparer.Default) == true);

            foreach (var interfaceMethod in interfaceImplementations)
            {
                if (HasMustUseResultAttributeCore(interfaceMethod, compilation))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasMustUseResultAttributeCore(IMethodSymbol method, Compilation compilation)
        {
            return method.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name == "MustUseResultAttribute" ||
                // Support for JetBrains annotations
                attr.AttributeClass?.Name == "MustUseReturnValueAttribute" ||
                attr.AttributeClass?.IsClrType(compilation, typeof(Core.Attributes.MustUseResultAttribute)) == true);
        }
    }
}
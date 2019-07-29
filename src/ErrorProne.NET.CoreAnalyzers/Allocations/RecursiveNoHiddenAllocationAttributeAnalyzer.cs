using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using ErrorProne.NET.AsyncAnalyzers;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

#nullable enable

namespace ErrorProne.NET.CoreAnalyzers.Allocations
{
    /// <summary>
    /// Analyzer that warns when the result of a method invocation is ignore (when it potentially, shouldn't).
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RecursiveNoHiddenAllocationAttributeAnalyzer : DiagnosticAnalyzer
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.LinqAllocation;

        private static readonly string Title = "Recursive hidden application enforcement.";
        private static readonly string Message = "{0} \"{1}\" is being called from within a recursive NoHiddenAllocations region and is not marked with NoHiddenAllocations.";

        private static readonly string Description = "A region which is marked with NoHiddenAllocations(Recursive = true) requires its callers to also be marked with the NoHiddenAllocations attribute.";
        private const string Category = "CodeSmell";

        // Using warning for visibility purposes
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        public static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, Message, Category, Severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <nodoc />
        public RecursiveNoHiddenAllocationAttributeAnalyzer() 
        {
        }

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            context.RegisterOperationAction(AnalyzeInvocationOperation, OperationKind.Invocation);
            context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);

            context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
        }

        private void AnalyzeObjectCreation(OperationAnalysisContext context)
        {
            if (NoHiddenAllocationsConfiguration.ShouldNotEnforceRecursiveApplication(context.Operation))
            {
                return;
            }

            var operation = (IObjectCreationOperation)context.Operation;

            if (operation.Constructor.IsImplicitlyDeclared)
            {
                return;
            }

            if (NoHiddenAllocationsConfiguration.ShouldNotDetectAllocationsFor(operation.Constructor))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, operation.Syntax.GetLocation(), "Constructor for", operation.Type.Name));
            }
        }

        private void AnalyzePropertyReference(OperationAnalysisContext context)
        {
            if (NoHiddenAllocationsConfiguration.ShouldNotEnforceRecursiveApplication(context.Operation))
            {
                return;
            }

            var operation = (IPropertyReferenceOperation)context.Operation;

            var propertySymbol = operation.Property;

            var propertyDeclaration = propertySymbol.TryGetDeclarationSyntax();

            // Can't figure out how to easily tell if it's a getter or a setter being called, so only check for attributes at the property level.
            // This effectively ignores attributes at the getter / setter level.
            if (propertyDeclaration != null && NoHiddenAllocationsConfiguration.ShouldNotDetectAllocationsFor(propertyDeclaration, operation.SemanticModel))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, operation.Syntax.GetLocation(), "Property", propertySymbol.Name));
            }
        }

        private void AnalyzeInvocationOperation(OperationAnalysisContext context)
        {
            if (NoHiddenAllocationsConfiguration.ShouldNotEnforceRecursiveApplication(context.Operation))
            {
                return;
            }

            var operation = ((IInvocationOperation) context.Operation);
            var targetSyntax = operation.TargetMethod.TryGetDeclarationSyntax();

            if (NoHiddenAllocationsConfiguration.ShouldNotDetectAllocationsFor(targetSyntax, context.Operation.SemanticModel))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, operation.Syntax.GetLocation(), "Method", operation.TargetMethod.Name));
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// Analyzer warns when a struct with non-default invariants is constructed via default construction.
    /// For instance <code>ImmutableArray&lt;int&gt; a = default; int x = a.Count; will fail with NRE.</code>
    /// </summary>
    /// <remarks>
    /// Technically this analyzer belongs to StructAnalyzers project, but because its more important
    /// and a bit more common, I decided to put it into the set of core analyzers.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DoNotUseDefaultConstructorForStructAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.DoNotUseDefaultConstructionForStruct;

        private const string DoNotUseDefaultConstructionAttributeName = "DoNotUseDefaultConstructionAttribute";

        private static readonly string Title = $"Do not use default construction for struct marked with `{DoNotUseDefaultConstructionAttributeName}`.";

        private static readonly string Message =
            $"Do not use default construction for struct '{{0}}' marked with `{DoNotUseDefaultConstructionAttributeName}`.";
        private static readonly string Description = $"Structs marked with `{DoNotUseDefaultConstructionAttributeName}` should be constructed using a non-default constructor.";
        
        private const string Category = "CodeSmell";

        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        private static readonly List<Type> _specialTypes = new List<Type>
        {
            typeof(ImmutableArray<>)
        };

        /// <nodoc />
        private static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, Title, Category, Severity, isEnabledByDefault: true, description: Description);

        /// <nodoc />
        public DoNotUseDefaultConstructorForStructAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
            context.RegisterOperationAction(AnalyzeAssignment, OperationKind.SimpleAssignment);
            context.RegisterOperationAction(AnalyzeDefaultValue, OperationKind.DefaultValue);

        }

        private void AnalyzeDefaultValue(OperationAnalysisContext context)
        {
            var operation = (IDefaultValueOperation)context.Operation;
            ReportDiagnosticForTypeIfNeeded(context, operation.Syntax, operation.Type);
        }

        private void AnalyzeAssignment(OperationAnalysisContext context)
        {
            
        }

        private void AnalyzeObjectCreation(OperationAnalysisContext context)
        {
            var operation = (IObjectCreationOperation)context.Operation;
            ReportDiagnosticForTypeIfNeeded(context, operation.Syntax, operation.Type);
        }

        private static void ReportDiagnosticForTypeIfNeeded(
            OperationAnalysisContext context,
            SyntaxNode syntax,
            ITypeSymbol type)
        {
            if (HasDoNotUseDefaultConstructionOrSpecial(context.Compilation, type))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, syntax.GetLocation(), type.Name));
            }
        }

        private static bool HasDoNotUseDefaultConstructionOrSpecial(Compilation compilation, ITypeSymbol type)
        {
            var attributes = type.GetAttributes();
            if (attributes.Any(a => a.AttributeClass.Name.StartsWith(DoNotUseDefaultConstructionAttributeName)))
            {
                return true;
            }

            if (_specialTypes.Any(t => type.IsClrType(compilation, t)))
            {
                return true;
            }

            return false;
        }
    }
}
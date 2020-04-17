using System;
using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.StructAnalyzers
{
    /// <summary>
    /// Emits a diagnostic if a struct can be readonly.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MakeStructReadOnlyAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = DiagnosticIds.MakeStructReadonlyDiagnosticId;

        private static readonly string Title = "A struct can be made readonly";
        private static readonly string MessageFormat = "Struct '{0}' can be made readonly";
        private static readonly string Description = "Readonly structs have better performance when passed/return by readonly reference.";
        private const string Category = "Performance";
        
        // Using warning for visibility purposes
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterSymbolAction(AnalyzeStructDeclaration, SymbolKind.NamedType);
        }

        private void AnalyzeStructDeclaration(SymbolAnalysisContext context)
        {
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            if (!namedTypeSymbol.IsValueType || namedTypeSymbol.TypeKind == TypeKind.Enum)
            {
                return;
            }

            if (namedTypeSymbol.IsReadOnlyStruct())
            {
                return;
            }

            if (!context.TryGetSemanticModel(out var model))
            {
                return;
            }

            // Struct can be readonly when all the instance fields and properties are readonly.
            var members = namedTypeSymbol
                .GetMembers()
                .Where(m => !m.IsStatic)
                .Where(f => f is IFieldSymbol || f is IPropertySymbol || f is IMethodSymbol).ToList();
            
            // If there is a 'this' assignment, like void Foo() => this = new MyStruct();
            // then the struct can't be readonly.
            if (members.All(m => IsReadonly(m)) && 
                !members.Any(m => HasAssignmentToThis(m, model)))
            {
                var diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }

        //public readonly struct S
        //{
        //    public void Foo(S s)
        //    {
        //        this = s;
        //    }
        //}
        private bool HasAssignmentToThis(ISymbol symbol, SemanticModel model)
        {
            if (symbol.IsConstructor())
            {
                // Assignments in constructors are fine.
                return false;
            }

            var syntax = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            if (syntax == null)
            {
                return false;
            }

            // Unfortunately, an approach based on dataflow analysis doesn't work,
            // because in the following two cases WrittenInside property contains
            // IParameterSymbol with IsThis equals true:
            // public void M(SelfAssign other) => 
            //    this = other; // <=== here
            // public int X {get;}
            // public int GetX() => X; // <=== here
            // So using syntax based approach instead.

            bool hasThisAssignment = syntax.DescendantNodesAndSelf()
                .OfType<AssignmentExpressionSyntax>()
                .Any(a => a.Left is ThisExpressionSyntax);

            if (hasThisAssignment)
            {
                return true;
            }

            // Now another crazy case:
            // ref MyStruct this_ = ref this;
            // this_ = new MyStruct();

            // So, if we see 'ref this' we won't emit a warning.

            // But we need to consider two cases:
            // ref readonly MyStruct this_ = ref this; // fine

            foreach (var refExpression in syntax.DescendantNodesAndSelf()
                .OfType<RefExpressionSyntax>())
            {
                var operation = model.GetOperation(refExpression.Parent);
                if (operation.Parent is IVariableDeclaratorOperation decl)
                {
                    if (decl.Symbol != null && decl.Symbol.IsRef && decl.Symbol.RefKind == RefKind.In)
                    {
                        // this is 'ref readonly' case.
                        return false;
                    }
                }

                // It seems that we have 'ref this' but it is not used
                // in 'ref readonly MyType = ' assignment.
                return true;
            }


            // The old version that doesn't work.
            //var dataFlows = syntax.AnalyzeDataflow(model);

            //bool hasThisAssignment =
            //    dataFlows.Where(df => df != null && df.Succeeded)
            //        .Any(a => a.WrittenInside.Any(w => w is IParameterSymbol ps && ps.IsThis));
            return false;
        }

        private bool IsReadonly(ISymbol member)
        {
            switch (member)
            {
                case IFieldSymbol fs:
                    return fs.IsReadOnly;
                case IPropertySymbol ps:
                    // Property is readonly, like 'public int X {get;}' or has an explicit setter.
                    return ps.IsReadOnly || ps.SetMethod != null;
                case IMethodSymbol ms:
                    return true;
                default:
                    throw new InvalidOperationException($"Unknown member type '{member.GetType()}'.");
            }
        }
    }
}
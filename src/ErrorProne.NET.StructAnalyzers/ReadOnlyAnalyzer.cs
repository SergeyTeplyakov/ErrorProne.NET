using System;
using System.Collections.Generic;
using System.Linq;
using ErrorProne.NET.Core;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.StructAnalyzers
{
    public static class ReadOnlyAnalyzer
    {
        public static bool MemberCanBeReadOnly(SyntaxNode syntax, SemanticModel semanticModel)
        {
            return syntax switch
            {
                PropertyDeclarationSyntax p => PropertyCanBeReadOnly(p, semanticModel),
                MethodDeclarationSyntax m => MethodCanBeReadOnly(m, semanticModel),
                _ => false,
            };
        }

        public static bool StructCanBeReadOnly(INamedTypeSymbol namedTypeSymbol, SemanticModel semanticModel)
        {
            if (!namedTypeSymbol.IsValueType || namedTypeSymbol.TypeKind == TypeKind.Enum)
            {
                return false;
            }

            if (namedTypeSymbol.IsReadOnlyStruct())
            {
                return false;
            }

            // Struct can be readonly when all the instance fields and properties are readonly.
            var members = namedTypeSymbol
                .GetMembers()
                .Where(m => !m.IsStatic)
                .Where(f => f is IFieldSymbol || f is IPropertySymbol || (f is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary))
                .Select(s => new {Symbol = s, Syntax = s.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()!})
                .Where(s => s.Syntax != null)
                .ToList();

            // If there is a 'this' assignment, like void Foo() => this = new MyStruct();
            // then the struct can't be readonly.
            if (members.All(m => IsReadonly(m.Symbol) || MemberCanBeReadOnly(m.Syntax, semanticModel)))
            {
                return true;
            }

            return false;
        }

        private static bool IsReadonly(ISymbol member)
        {
            return member switch
            {
                IFieldSymbol fs => fs.IsReadOnly,
                IPropertySymbol ps => ps.MarkedWithReadOnlyModifier(),
                IMethodSymbol m => m.IsReadOnly || m.IsConstructor(),
                _ => throw new InvalidOperationException($"Unknown member type '{member.GetType()}'."),
            };
        }

        public static bool PropertyCanBeReadOnly(PropertyDeclarationSyntax property, SemanticModel semanticModel)
        {
            var propertyInfo = semanticModel.GetDeclaredSymbol(property);
            if (propertyInfo == null
                || propertyInfo.IsStatic
                || property.IsGetSetAutoProperty() // int Prop {get;set;} property can't be readonly but int Prop {get;} - can!
                || propertyInfo.ContainingType.IsReadOnly
                || !propertyInfo.ContainingType.IsValueType
                || property.MarkedWithReadOnlyModifier()
            )
            {
                return false;
            }

            return !HasAssignmentToThis(propertyInfo, semanticModel);
        }


        public static bool MethodCanBeReadOnly(MethodDeclarationSyntax method, SemanticModel semanticModel)
        {
            var methodSymbol = semanticModel.GetDeclaredSymbol(method);

            if (methodSymbol == null || methodSymbol.IsStatic || methodSymbol.IsReadOnly || !methodSymbol.ContainingType.IsValueType)
            {
                return false;
            }

            return !HasAssignmentToThis(methodSymbol, semanticModel);
        }

        private static bool HasAssignmentToThis(ISymbol symbol, SemanticModel model)
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

            // Unfortunately, the approach solely based on dataflow analysis doesn't work
            // because its impossible to separate the following cases:
            // ref readonly S s = ref this; // fine
            // ref S s = ref this; // not fine

            // So we use a mixture of syntax-based and the dataflow-based approaches.
            var mutations = syntax
                .DescendantNodesAndSelf()
                // Looking for all expressions but for invocations.
                // All the invocations and the arguments covered separately.
                .Where(n => n is ExpressionSyntax && !(n is InvocationExpressionSyntax))
                .ToList();

            foreach (var m in mutations)
            {
                var dataFlow = model.AnalyzeDataFlow(m);
                if (dataFlow.Succeeded && 
                    dataFlow.WrittenInside
                        .Any(df => df is IParameterSymbol p && p.IsThis && (p.RefKind == RefKind.Ref || p.RefKind == RefKind.Out)))
                {
                    return true;
                }
            }

            // Here we're checking for 'ref this' and 'in this' cases.
            foreach (var expression in syntax.DescendantNodesAndSelf().Where(n => n is ArgumentSyntax || n is RefExpressionSyntax))
            {
                if (expression is RefExpressionSyntax)
                {
                    if (expression.Parent != null)
                    {
                        var operation = model.GetOperation(expression.Parent);
                        if (operation?.Parent is IVariableDeclaratorOperation decl)
                        {
                            if (decl.Symbol != null && decl.Symbol.IsRef && decl.Symbol.RefKind == RefKind.In)
                            {
                                // this is 'ref readonly' case.
                                return false;
                            }
                        }
                    }

                    // It seems that we have 'ref this' but it is not used
                    // in 'ref readonly MyType = ' assignment.
                    return true;
                }

                // this is ArgumentSyntax
                if (model.GetOperation(expression) is IArgumentOperation argument &&
                    argument.Parameter.RefKind == RefKind.Ref)
                {
                    // this is 'FooBar(ref this)' case.
                    return true;
                }
            }

            return false;
        }
    }
}
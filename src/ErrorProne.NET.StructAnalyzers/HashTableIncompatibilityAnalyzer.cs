using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Core;
using ErrorProne.NET.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
    
namespace ErrorProne.NET.StructAnalyzers
{
    /// <summary>
    /// An analyzer that warns when a struct with default implementation of <see cref="Object.Equals(object)"/> or <see cref="Object.GetHashCode()"/> are used as a key in a hash table.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class HashTableIncompatibilityAnalyzer : DiagnosticAnalyzer
    {
        private static readonly Type[] Types = {
            typeof(HashSet<>),
            typeof(ISet<>),
            typeof(IDictionary<,>),
            typeof(IReadOnlyDictionary<,>),
            typeof(Dictionary<,>),
            typeof(ConcurrentDictionary<,>),
            typeof(ImmutableHashSet<>),
            typeof(ImmutableDictionary<,>)
        };

        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.HashTableIncompatibilityDiagnosticId;

        private static readonly string Title = "Hash table unfriendly type is used in a hash table";
        private static readonly string MessageFormat = "Struct '{0}' with default {1} implementation is used as a key in a hash table.";
        private static readonly string Description = "Default implementation of Equals/GetHashCode for struct is inneficient and could cause severe performance issues.";
        private const string Category = "Performance";
        
        // Using warning for visibility purposes
        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        private static readonly Dictionary<string, int> _wellKnownHashTableTypes = new Dictionary<string, int>(WellKnownHashTables().ToDictionary(t => t.name.Remove(t.name.LastIndexOf("`")), t => t.arity));

        /// <nodoc />
        public static readonly DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);
        
        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
            context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
            context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);

            context.RegisterSyntaxNodeAction(AnalyzeUsingDirective, SyntaxKind.UsingDirective);
            context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeLocalFunction, SyntaxKind.LocalFunctionStatement);
            context.RegisterSyntaxNodeAction(AnalyzeLocal, SyntaxKind.LocalDeclarationStatement);
        }

        private void AnalyzeField(SymbolAnalysisContext context)
        {
            if (context.Symbol is IFieldSymbol fs && fs.Type != null && fs.TryGetDeclarationSyntax() is var syntax && syntax != null)
            {
                DoAnalyzeType(fs.Type, syntax.Type.GetLocation(), d => context.ReportDiagnostic(d));
            }
        }

        private void AnalyzeProperty(SymbolAnalysisContext context)
        {
            if (context.Symbol is IPropertySymbol fs && fs.TryGetDeclarationSyntax() is var syntax && syntax != null)
            {
                DoAnalyzeType(fs.Type, syntax.Type.GetLocation(), d => context.ReportDiagnostic(d));
            }
        }

        private void AnalyzeLocal(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is LocalDeclarationStatementSyntax vs)
            {
                foreach (var local in vs.Declaration.Variables)
                {
                    if (context.SemanticModel.GetDeclaredSymbol(local) is ILocalSymbol resolvedLocal)
                    {
                        DoAnalyzeType(resolvedLocal.Type, vs.Declaration.Type.GetLocation(), d => context.ReportDiagnostic(d));
                    }
                }
            }
        }

        private void AnalyzeLocalFunction(SyntaxNodeAnalysisContext context)
        {
            if (context.SemanticModel.GetDeclaredSymbol(context.Node) is IMethodSymbol methodSymbol)
            {
                var methodDeclarationSyntax = (LocalFunctionStatementSyntax) context.Node;
                if (!methodSymbol.ReturnsVoid)
                {
                    DoAnalyzeType(methodSymbol.ReturnType, methodDeclarationSyntax.ReturnType.GetLocation(), d => context.ReportDiagnostic(d));
                }

                int idx = 0;
                foreach (var p in methodSymbol.Parameters)
                {
                    DoAnalyzeType(p.Type, methodDeclarationSyntax.ParameterList.Parameters[idx].Type.GetLocation(), d => context.ReportDiagnostic(d));
                    idx++;
                }
            }
        }

        private void AnalyzeUsingDirective(SyntaxNodeAnalysisContext context)
        {
            var usingDirective = (UsingDirectiveSyntax) context.Node;
            if (context.SemanticModel.GetDeclaredSymbol(usingDirective) is IAliasSymbol alias)
            {
                if (alias.Target is ITypeSymbol ts)
                {
                    DoAnalyzeType(ts, usingDirective.Name.GetLocation(), d => context.ReportDiagnostic(d));
                }
            }
        }

        private void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax) context.Node;
            if (classDeclaration.BaseList != null && 
                context.SemanticModel.GetDeclaredSymbol(context.Node) is ITypeSymbol methodSymbol)
            {
                foreach (var baseType in classDeclaration.BaseList.Types)
                {
                    var type = context.SemanticModel.GetTypeInfo(baseType.Type).Type;
                    if (type != null)
                    {
                        DoAnalyzeType(type, baseType.GetLocation(), d => context.ReportDiagnostic(d));
                    }
                }
            }
        }

        private void AnalyzeMethod(SymbolAnalysisContext context)
        {
            if (context.Symbol is IMethodSymbol ms && 
                ms.TryGetDeclarationSyntax() is var syntax &&
                syntax != null)
            {
                if (!ms.ReturnsVoid)
                {
                    DoAnalyzeType(ms.ReturnType, syntax.ReturnType.GetLocation(), d => context.ReportDiagnostic(d));
                }

                int idx = 0;
                foreach (var p in ms.Parameters)
                {
                    DoAnalyzeType(p.Type, syntax.ParameterList.Parameters[idx].Type.GetLocation(), d => context.ReportDiagnostic(d));
                    idx++;
                }
            }
        }

        private void DoAnalyzeType(ITypeSymbol type, Location location, Action<Diagnostic> diagnosticsReporter)
        {
            if (type is IArrayTypeSymbol at)
            {
                DoAnalyzeType(at.ElementType, location, diagnosticsReporter);
                return;
            }

            if (type is INamedTypeSymbol ts && ts.IsGenericType && IsWellKnownHashTableType(ts))
            {
                // Key is always a first argument.
                var key = ts.TypeArguments[0];
                if (key is INamedTypeSymbol namedKey && namedKey.IsTuple())
                {
                    key = namedKey.GetTupleElements()
                              .FirstOrDefault(t =>
                                  t.IsStruct() && t.HasDefaultEqualsOrHashCodeImplementations(out _)) ?? key;
                }

                if (key.IsStruct() && key.HasDefaultEqualsOrHashCodeImplementations(out var equalsOrHashCode))
                {
                    string equalsOrHashCodeAsString = GetDescription(equalsOrHashCode);
                    var diagnostic = Diagnostic.Create(Rule, location, key.ToDisplayString(), equalsOrHashCodeAsString);
                    diagnosticsReporter(diagnostic);
                }
            }
        }

        private string GetDescription(ValueTypeEqualityImplementations equalsOrHashCode)
        {
            switch (equalsOrHashCode)
            {
                case ValueTypeEqualityImplementations.Equals:
                    return nameof(Equals);
                case ValueTypeEqualityImplementations.GetHashCode:
                    return nameof(GetHashCode);
                case ValueTypeEqualityImplementations.All:
                    return $"{nameof(Equals)} and {nameof(GetHashCode)}";
                default:
                    throw new InvalidOperationException($"Invalid value '{equalsOrHashCode}'");
            }
        }

        private static readonly SymbolDisplayFormat _symbolDisplayFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        private static bool IsWellKnownHashTableType(INamedTypeSymbol type)
        {
            if (type.ConstructedFrom != null &&
                type.ConstructedFrom.ToDisplayString(_symbolDisplayFormat) is var name &&
                _wellKnownHashTableTypes.ContainsKey(name))
            {
                return true;
            }

            return false;
        }
        
        private static (string name, int arity)[] WellKnownHashTables()
        {
            return Types.Select(t => (t.FullName, t.GenericTypeArguments.Length)).ToArray();
        }
    }
}
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Structs
{
    /// <summary>
    /// An analyzer that warns when a struct with default implementation of <see cref="Object.Equals(object)"/> or <see cref="Object.GetHashCode()"/> are used as a key in a hash table.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class HashTableIncompatibilityAnalyzer : DiagnosticAnalyzer
    {
        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.HashTableIncompatibilityDiagnosticId;

        private static readonly string Title = "Hash table unfriendly type is used in a hash table";
        private static readonly string MessageFormat = "Struct '{0}' with default Equals/GetHashCode implementation used as a key in a hash table '1'";
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
            context.RegisterSymbolAction(AnalyzeType, SymbolKind.Field);
            //context.RegisterSyntaxNodeAction(AnalyzeDottedExpression, SyntaxKind.type);
            //context.RegisterSyntaxNodeAction(AnalyzeElementAccessExpression, SyntaxKind.ElementAccessExpression);
        }

        private void AnalyzeType(SymbolAnalysisContext context)
        {
            if (context.TryGetSemanticModel(out var semanticModel))
            {
                if (context.Symbol is IFieldSymbol fs)
                {
                    DoAnalyzeType(fs.Type, d => context.ReportDiagnostic(d));
                }
            }
        }

        private void DoAnalyzeType(ITypeSymbol type, Action<Diagnostic> diagnosticsReporter)
        {
            if (type is INamedTypeSymbol ts && ts.IsGenericType && IsWellKnownHashTableType(ts))
            {
                // Key is always a first argument.
                var key = ts.TypeArguments[0];

                if (DefaultEqualsOrHashCodeImplementations(key, out _))
                {
                    var diagnostic = Diagnostic.Create(Rule, type.Locations[0], type.ToDisplayString());
                    diagnosticsReporter(diagnostic);
                }
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
            var types = new[]
            {
                typeof(HashSet<>),
                typeof(Dictionary<,>),
                typeof(ConcurrentDictionary<,>),
                typeof(ImmutableHashSet<>),
                typeof(ImmutableDictionary<,>)
            };

            return types.Select(t => (t.FullName, t.GenericTypeArguments.Length)).ToArray();
        }

        public enum EqualsOrHashCode
        {
            Equals,
            HashCode,
        }

        private static bool DefaultEqualsOrHashCodeImplementations(ITypeSymbol type,
            out EqualsOrHashCode equalsOrHashCode)
        {

        }
    }
}
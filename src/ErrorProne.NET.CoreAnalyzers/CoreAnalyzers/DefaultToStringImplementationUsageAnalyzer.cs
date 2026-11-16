using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.AsyncAnalyzers
{
    /// <summary>
    /// The analyzer warns when a task instance is implicitly converted to string.
    /// </summary>
    /// <remarks>
    /// Here is an example: <code>var r = FooAsync(); Console.WriteLine($"r: {r}");</code>
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DefaultToStringImplementationUsageAnalyzer : AbstractDefaultToStringImplementationUsageAnalyzer
    {
        /// <nodoc />
        public static DiagnosticDescriptor Rule => DiagnosticDescriptors.EPC20;

        /// <inheritdoc />
        /// <remarks>
        /// Analyze generated code so diagnostics can still be reported in authored partial
        /// methods and mapped Razor source. Generated diagnostics are filtered separately.
        /// </remarks>
        public override bool ReportDiagnosticsOnGeneratedCode { get; } = true;

        /// <nodoc />
        public DefaultToStringImplementationUsageAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override bool ShouldReportDiagnostic(OperationAnalysisContext context, Diagnostic diagnostic)
        {
            var location = diagnostic.Location;

            // Keep diagnostics without a source tree instead of applying source-based filters.
            if (location.SourceTree is not SyntaxTree tree)
            {
                return true;
            }

            // Suppress diagnostics in hidden regions, such as generated Razor scaffolding.
            if (tree.GetLineVisibility(location.SourceSpan.Start, context.CancellationToken) == LineVisibility.Hidden)
            {
                return false;
            }

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree);
            // An explicit generated_code = true takes precedence over Razor source mapping.
            if (options.TryGetValue("generated_code", out var value) &&
                bool.TryParse(value, out var explicitlyGenerated) && explicitlyGenerated)
            {
                return false;
            }

            var mappedSpan = location.GetMappedLineSpan();
            // Report diagnostics mapped back to authored Razor or cshtml source files.
            if (mappedSpan.HasMappedPath &&
                (mappedSpan.Path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) ||
                 mappedSpan.Path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // Otherwise, report only when neither the file nor the symbol is generated.
            return !IsGeneratedFile(tree, context.Options, context.CancellationToken) &&
                   !IsGeneratedSymbol(context.ContainingSymbol, context);
        }

        // Roslyn may mark every context as non-generated when all analyzers opt in to generated code.
        private static bool IsGeneratedFile(SyntaxTree tree, AnalyzerOptions options, CancellationToken cancellationToken)
        {
            var treeOptions = options.AnalyzerConfigOptionsProvider.GetOptions(tree);
            if (treeOptions.TryGetValue("generated_code", out var value) &&
                bool.TryParse(value, out var explicitlyGenerated))
            {
                return explicitlyGenerated;
            }

            var fileName = Path.GetFileName(tree.FilePath.Replace('\\', '/'));
            if (!string.IsNullOrEmpty(fileName))
            {
                if (fileName.StartsWith("TemporaryGeneratedFile_", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                if (!string.IsNullOrEmpty(nameWithoutExtension) &&
                    (nameWithoutExtension.EndsWith(".designer", StringComparison.OrdinalIgnoreCase) ||
                     nameWithoutExtension.EndsWith(".generated", StringComparison.OrdinalIgnoreCase) ||
                     nameWithoutExtension.EndsWith(".g", StringComparison.OrdinalIgnoreCase) ||
                     nameWithoutExtension.EndsWith(".g.i", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            foreach (var trivia in tree.GetRoot(cancellationToken).GetLeadingTrivia())
            {
                if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) &&
                    !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) &&
                    !trivia.IsKind(SyntaxKind.ShebangDirectiveTrivia))
                {
                    continue;
                }

                var text = trivia.ToString();
                if (text.IndexOf("<auto-generated", StringComparison.Ordinal) >= 0 ||
                    text.IndexOf("<autogenerated", StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsGeneratedSymbol(ISymbol? symbol, OperationAnalysisContext context)
        {
            var generatedCodeAttribute = context.Compilation.GetTypeByMetadataName("System.CodeDom.Compiler.GeneratedCodeAttribute");
            if (generatedCodeAttribute is null)
            {
                return false;
            }

            while (symbol is not null)
            {
                // Skip attributes on partial symbols, but keep walking to their containing types.
                if (symbol.Kind != SymbolKind.Namespace &&
                    symbol.DeclaringSyntaxReferences.Length <= 1)
                {
                    foreach (var attribute in symbol.GetAttributes())
                    {
                        if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, generatedCodeAttribute))
                        {
                            continue;
                        }

                        // Ignore a declaration-part attribute when analyzing the authored implementation.
                        if (symbol is IMethodSymbol { PartialDefinitionPart: IMethodSymbol definition } &&
                            attribute.ApplicationSyntaxReference is SyntaxReference syntaxReference &&
                            definition.DeclaringSyntaxReferences.Any(declaration =>
                                declaration.SyntaxTree == syntaxReference.SyntaxTree &&
                                declaration.Span.Contains(syntaxReference.Span)))
                        {
                            continue;
                        }

                        return true;
                    }
                }

                // Check the property or event associated with an accessor before its containing type.
                symbol = symbol is IMethodSymbol method && method.AssociatedSymbol is ISymbol associatedSymbol
                    ? associatedSymbol
                    : symbol.ContainingSymbol;
            }

            return false;
        }

        /// <inheritdoc />
        protected override bool TryCreateDiagnostic(Compilation compilation, ITypeSymbol type, Location location, [NotNullWhen(true)]out Diagnostic? diagnostic)
        {
            diagnostic = null;

            if (type.SpecialType == SpecialType.System_Object)
            {
                // Its fine to call 'ToString' or do the conversion from 'object'.
                return false;
            }

            if (type.IsAbstract)
            {
                // We can't emit a warning for abstract types or interfaces, because we don't know if the derive type would have a custom ToString impl or not.
                return false;
            }

            if (type.TypeKind == TypeKind.TypeParameter)
            {
                // Can't assume anything about the generic types as well.
                return false;
            }

            if (HasImplicitConversionToString(type))
            {
                // The type opts into a custom string representation via 'public static implicit operator string',
                // so a default ToString impl is not what will actually be used. See issue #317.
                return false;
            }

            if (NoToStringOverride(type, out var typeWithNoToString))
            {
                diagnostic = Diagnostic.Create(Rule, location, typeWithNoToString);
            }

            return diagnostic != null;
        }

        private static bool HasImplicitConversionToString(ITypeSymbol type)
        {
            foreach (var member in type.GetMembers(WellKnownMemberNames.ImplicitConversionName))
            {
                if (member is IMethodSymbol { MethodKind: MethodKind.Conversion } method &&
                    method.ReturnType.SpecialType == SpecialType.System_String)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool NoToStringOverride(ITypeSymbol type, [NotNullWhen(true)]out ITypeSymbol? typeWithNoToString)
        {
            typeWithNoToString = null;

            if (type.IsTupleType && type is INamedTypeSymbol namedType)
            {
                // Getting the first type from the tuple with no ToString impl.
                var noToString = namedType.TupleElements.Select(te => te.Type).Where(t => !t.OverridesToString());
                typeWithNoToString = noToString.FirstOrDefault();
            }
            else
            {
                if (!type.OverridesToString())
                {
                    typeWithNoToString = type;
                }
            }

            return typeWithNoToString != null;
        }
    }
}
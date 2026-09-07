using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ErrorProne.NET.Annotations.Generation;

[Generator(LanguageNames.CSharp)]
public sealed class AnnotationsGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MissingNamespace = new(
        "EPANN001", "Annotation namespace is unavailable",
        "Set RootNamespace or ErrorProneAnnotationsNamespace and include the annotations package build assets",
        "Configuration", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor InvalidNamespace = new(
        "EPANN002", "Annotation namespace is invalid",
        "'{0}' is not a valid annotation namespace", "Configuration", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor AmbiguousAttribute = new(
        "EPANN003", "Existing annotation definitions are ambiguous",
        "Multiple accessible definitions of '{0}' exist in {1}; set ErrorProneAnnotationsNamespace or remove the conflicting reference",
        "Configuration", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly (string Name, string Targets, string Summary)[] Attributes =
    {
        ("AcquiresOwnershipAttribute", "Parameter", "Transfers cleanup responsibility to the receiving method."),
        ("ReturnsOwnershipAttribute", "Method | Property | ReturnValue", "Transfers cleanup responsibility to the recipient of the result."),
        ("DoNotDisposeAttribute", "Parameter | Field | Property | Method | ReturnValue", "Marks a borrowed value that the recipient must not dispose or transfer."),
        ("KeepsOwnershipAttribute", "Method | Property | ReturnValue", "Compatibility annotation for a borrowed result; prefer DoNotDispose."),
        ("NoOwnershipAttribute", "Parameter | Field | Property", "Compatibility annotation for borrowed parameters and members; prefer DoNotDispose."),
        ("MustUseResultAttribute", "Method", "Requires the caller to observe the method result."),
        ("MustUseReturnValueAttribute", "Method", "Compatibility annotation for a method result that must be observed; prefer MustUseResult."),
        ("UseConfigureAwaitFalseAttribute", "Assembly", "Requires ConfigureAwait(false) for awaits in this assembly."),
        ("DoNotUseConfigureAwaitAttribute", "Assembly", "Marks ConfigureAwait(false) as redundant for awaits in this assembly."),
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(context.CompilationProvider.Combine(context.AnalyzerConfigOptionsProvider), static (output, input) =>
        {
            var compilation = input.Left;
            var options = input.Right.GlobalOptions;
            if (!options.TryGetValue("build_property.ErrorProneAnnotationsNamespace", out var configuredNamespace)
                || string.IsNullOrWhiteSpace(configuredNamespace))
            {
                if (!options.TryGetValue("build_property.RootNamespace", out configuredNamespace))
                {
                    output.ReportDiagnostic(Diagnostic.Create(MissingNamespace, Location.None));
                    return;
                }
            }

            if (!TryGetNamespace(configuredNamespace, out var sourceNamespace, out var metadataNamespace))
            {
                output.ReportDiagnostic(Diagnostic.Create(InvalidNamespace, Location.None, configuredNamespace));
                return;
            }

            // Accessibility alone does not make extern-alias-only types available to generated source.
            var globalAssemblies = new HashSet<IAssemblySymbol>(compilation.References
                .Where(reference => reference.Properties.Aliases.IsDefaultOrEmpty
                    || reference.Properties.Aliases.Contains("global"))
                .Select(reference => compilation.GetAssemblyOrModuleSymbol(reference))
                .OfType<IAssemblySymbol>(), SymbolEqualityComparer.Default);

            foreach (var attribute in Attributes)
            {
                output.CancellationToken.ThrowIfCancellationRequested();
                var metadataName = metadataNamespace.Length == 0
                    ? attribute.Name : metadataNamespace + "." + attribute.Name;
                var existing = compilation.GetTypesByMetadataName(metadataName);
                if (attribute.Name is "UseConfigureAwaitFalseAttribute" or "DoNotUseConfigureAwaitAttribute")
                {
                    // These assembly policies also recognize legacy attribute classes without the suffix.
                    var legacyName = metadataName.Substring(0, metadataName.Length - nameof(Attribute).Length);
                    existing = existing.AddRange(compilation.GetTypesByMetadataName(legacyName)
                        .Where(type => IsAttributeType(type, compilation)));
                }

                if (existing.Any(type => SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly)))
                {
                    continue;
                }

                var accessible = existing.Where(type => globalAssemblies.Contains(type.ContainingAssembly)
                    && compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
                    .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToArray();
                if (accessible.Length > 1)
                {
                    var assemblies = string.Join(", ", accessible.Select(type => type.ContainingAssembly.Identity.ToString())
                        .OrderBy(name => name, StringComparer.Ordinal));
                    output.ReportDiagnostic(Diagnostic.Create(AmbiguousAttribute, Location.None, metadataName, assemblies));
                    continue;
                }

                if (accessible.Length == 1)
                {
                    continue;
                }

                // Internal copies stay local to each assembly, but their usages on public APIs
                // remain visible to downstream analyzers through metadata.
                var targets = "global::System.AttributeTargets."
                    + attribute.Targets.Replace(" | ", " | global::System.AttributeTargets.");
                var namespaceStart = sourceNamespace.Length == 0 ? "" : "namespace " + sourceNamespace + "\n{\n";
                var namespaceEnd = sourceNamespace.Length == 0 ? "" : "}\n";
                var source = "// <auto-generated/>\n" + namespaceStart + @"
    /// <summary>" + attribute.Summary + @"</summary>
    [global::System.AttributeUsage(" + targets + @", AllowMultiple = false, Inherited = true)]
    internal sealed class " + attribute.Name + @" : global::System.Attribute
    {
    }
" + namespaceEnd;
                output.AddSource(attribute.Name + ".g.cs", SourceText.From(source, Encoding.UTF8));
            }
        });
    }

    private static bool IsAttributeType(INamedTypeSymbol type, Compilation compilation)
    {
        var attributeType = compilation.GetTypeByMetadataName("System.Attribute");
        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, attributeType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetNamespace(string value, out string sourceNamespace, out string metadataNamespace)
    {
        value = value.Trim();
        sourceNamespace = metadataNamespace = "";
        if (value.Length == 0)
        {
            return true;
        }

        var escaped = string.Join(".", value.Split('.').Select(part =>
            SyntaxFacts.GetKeywordKind(part) != SyntaxKind.None ? "@" + part : part));
        var name = SyntaxFactory.ParseName(escaped);
        if (name.ContainsDiagnostics || name.DescendantNodesAndSelf().Any(node => node is AliasQualifiedNameSyntax or GenericNameSyntax))
        {
            return false;
        }

        var identifiers = name.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
            .Select(identifier => identifier.Identifier).ToArray();
        sourceNamespace = string.Join(".", identifiers.Select(identifier => identifier.Text));
        metadataNamespace = string.Join(".", identifiers.Select(identifier => identifier.ValueText));
        return identifiers.Length > 0;
    }
}

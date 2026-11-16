using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace ErrorProne.NET.DisposableAnalyzers;

internal enum OwnershipKind
{
    Unspecified,
    Owned,
    Borrowed,
}

/// <summary>
/// Resolves source contracts before external contracts; unspecified results are left to the analyzer.
/// Additional files named *.ownership.xml contain:
/// &lt;ownership&gt;&lt;member id="M:Namespace.Type.Method(System.IDisposable)"
/// assembly="AssemblyName" returns="owned|borrowed"&gt;
/// &lt;parameter name="resource" ownership="owned|borrowed"/&gt;
/// &lt;/member&gt;&lt;/ownership&gt;.
/// Assembly and returns are optional. Parameter names belong to the declared member, not its callers.
/// Assembly names are simple names, compared ordinally. Compatible assembly-specific entries supplement
/// unqualified entries; overlapping conflicting contracts are rejected. Unavailable libraries are allowed.
/// </summary>
internal sealed class OwnershipContracts
{
    private readonly ImmutableDictionary<string, ExternalContract> _external;

    private OwnershipContracts(ImmutableDictionary<string, ExternalContract> external,
        ImmutableArray<Diagnostic> diagnostics)
    {
        _external = external;
        Diagnostics = diagnostics;
    }

    public ImmutableArray<Diagnostic> Diagnostics { get; }

    public static OwnershipContracts Create(Compilation compilation, AnalyzerOptions options,
        CancellationToken cancellationToken)
    {
        var contracts = new Dictionary<string, ExternalContract>(StringComparer.Ordinal);
        var membersById = new Dictionary<string, List<ExternalContract>>(StringComparer.Ordinal);
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var file in options.AdditionalFiles.OrderBy(file => file.Path, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!file.Path.EndsWith(".ownership.xml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = file.GetText(cancellationToken);
            void Report(string message, XObject? node = null, int line = 1, int column = 1)
            {
                if (node is IXmlLineInfo info && info.HasLineInfo())
                {
                    line = info.LineNumber;
                    column = info.LinePosition;
                }

                var position = 0;
                if (text != null && line > 0 && line <= text.Lines.Count)
                {
                    var sourceLine = text.Lines[line - 1];
                    position = sourceLine.Start + Math.Min(Math.Max(column - 1, 0), sourceLine.Span.Length);
                }

                var span = new TextSpan(position, 0);
                var lineSpan = text?.Lines.GetLinePositionSpan(span) ?? default;
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.ERP045,
                    Location.Create(file.Path, span, lineSpan), message));
            }

            if (text == null)
            {
                Report("The additional file could not be read.");
                continue;
            }

            try
            {
                using var input = new StringReader(text.ToString());
                using var reader = XmlReader.Create(input, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                });
                var document = XDocument.Load(reader, LoadOptions.SetLineInfo);
                var root = document.Root!;
                if (root.Name != "ownership" || !ValidateShape(root, Array.Empty<string>(), Report))
                {
                    if (root.Name != "ownership")
                    {
                        Report("The root element must be 'ownership' without a namespace.", root);
                    }

                    continue;
                }

                foreach (var member in root.Elements())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (member.Name != "member")
                    {
                        Report($"Unknown element '{member.Name}'.", member);
                        continue;
                    }

                    if (!ValidateShape(member, new[] { "id", "assembly", "returns" }, Report))
                    {
                        continue;
                    }

                    var id = (string?)member.Attribute("id");
                    if (id == null || !DocumentationIdSyntax.IsValid(id))
                    {
                        Report($"Invalid method or property documentation ID '{id}'.", member);
                        continue;
                    }

                    var assembly = (string?)member.Attribute("assembly");
                    if (assembly != null && (string.IsNullOrWhiteSpace(assembly)
                        || assembly != assembly.Trim() || assembly.IndexOfAny(new[] { ',', '\\', '/' }) >= 0))
                    {
                        Report("Assembly must be a nonempty simple assembly name.", member);
                        continue;
                    }

                    var returnsText = (string?)member.Attribute("returns");
                    var returns = ParseOwnership(returnsText);
                    if (returnsText != null && returns == OwnershipKind.Unspecified)
                    {
                        Report($"Unknown return ownership '{returnsText}'.", member);
                        continue;
                    }

                    var parameters = ImmutableDictionary.CreateBuilder<string, OwnershipKind>(StringComparer.Ordinal);
                    var valid = true;
                    foreach (var parameter in member.Elements())
                    {
                        if (parameter.Name != "parameter")
                        {
                            Report($"Unknown element '{parameter.Name}'.", parameter);
                            valid = false;
                            continue;
                        }

                        if (!ValidateShape(parameter, new[] { "name", "ownership" }, Report))
                        {
                            valid = false;
                            continue;
                        }

                        var name = (string?)parameter.Attribute("name");
                        var ownership = ParseOwnership((string?)parameter.Attribute("ownership"));
                        if (string.IsNullOrWhiteSpace(name) || name != name!.Trim()
                            || ownership == OwnershipKind.Unspecified || parameter.HasElements)
                        {
                            Report("A parameter needs a name, ownership 'owned' or 'borrowed', and no child elements.", parameter);
                            valid = false;
                        }
                        else if (parameters.ContainsKey(name))
                        {
                            Report($"Duplicate parameter '{name}'.", parameter);
                            valid = false;
                        }
                        else
                        {
                            parameters.Add(name, ownership);
                        }
                    }

                    if (!valid)
                    {
                        continue;
                    }

                    if (returns == OwnershipKind.Unspecified && parameters.Count == 0)
                    {
                        Report("A member must specify return or parameter ownership.", member);
                        continue;
                    }

                    // Only validate binding when the target exists. Packs may also contain absent libraries.
                    var symbols = DocumentationCommentId.GetSymbolsForDeclarationId(id, compilation)
                        .Where(s => assembly == null || s.ContainingAssembly?.Name == assembly).ToArray();
                    if (symbols.Length != 0 && parameters.Keys.Any(name =>
                        !symbols.Any(s => GetParameters(s).Any(p => p.Name == name))))
                    {
                        Report($"A parameter name does not belong to '{id}'.", member);
                        continue;
                    }

                    var entry = new ExternalContract(id, assembly, returns, parameters.ToImmutable());
                    var key = Key(id, assembly);
                    if (contracts.ContainsKey(key))
                    {
                        Report($"Duplicate member contract '{id}'.", member);
                    }
                    else if (membersById.TryGetValue(id, out var existingMembers) && existingMembers.Any(existing =>
                        (existing.Assembly == null || assembly == null || existing.Assembly == assembly)
                        && Conflicts(existing, entry)))
                    {
                        Report($"Conflicting member contract '{id}'.", member);
                    }
                    else
                    {
                        contracts.Add(key, entry);
                        if (!membersById.TryGetValue(id, out var entries))
                        {
                            entries = new List<ExternalContract>();
                            membersById.Add(id, entries);
                        }

                        entries.Add(entry);
                    }
                }
            }
            catch (XmlException ex)
            {
                Report($"Invalid XML: {ex.Message}", line: ex.LineNumber, column: ex.LinePosition);
            }
        }

        return new OwnershipContracts(contracts.ToImmutableDictionary(StringComparer.Ordinal), diagnostics.ToImmutable());
    }

    public OwnershipKind GetReturnOwnership(ISymbol methodOrProperty)
    {
        var members = RelatedMembers(Normalize(methodOrProperty)).ToArray();
        foreach (var member in members)
        {
            var source = ReturnAttributeOwnership(member);
            if (source != OwnershipKind.Unspecified)
            {
                return source;
            }
        }

        foreach (var member in members)
        {
            foreach (var contract in ExternalContracts(member))
            {
                if (contract.Returns != OwnershipKind.Unspecified)
                {
                    return contract.Returns;
                }
            }
        }

        return OwnershipKind.Unspecified;
    }

    public bool AcquiresOwnership(IParameterSymbol parameter) => ParameterOwnership(parameter) == OwnershipKind.Owned;

    public bool IsBorrowed(ISymbol symbol)
    {
        return symbol is IParameterSymbol parameter
            ? ParameterOwnership(parameter) == OwnershipKind.Borrowed
            : HasBorrowedAttribute(symbol.GetAttributes())
                || (symbol is IMethodSymbol or IPropertySymbol && GetReturnOwnership(symbol) == OwnershipKind.Borrowed);
    }

    private OwnershipKind ParameterOwnership(IParameterSymbol parameter)
    {
        var ordinal = parameter.Ordinal;
        var owner = parameter.ContainingSymbol;
        if (owner is IMethodSymbol { ReducedFrom: { } reduced })
        {
            ordinal += reduced.Parameters.Length - ((IMethodSymbol)owner).Parameters.Length;
            owner = reduced;
        }

        // Accessor parameters and indexer arguments must resolve the same property contract.
        if (owner is IMethodSymbol { AssociatedSymbol: IPropertySymbol property }
            && ordinal < property.Parameters.Length)
        {
            owner = property;
        }

        owner = owner.OriginalDefinition;
        var members = RelatedMembers(owner).ToArray();
        var inherited = OwnershipKind.Unspecified;
        for (var i = 0; i < members.Length; i++)
        {
            var parameters = GetParameters(members[i]);
            if (ordinal >= parameters.Length)
            {
                continue;
            }

            var attributes = parameters[ordinal].GetAttributes();
            var source = HasBorrowedAttribute(attributes)
                ? OwnershipKind.Borrowed
                : HasAttribute(attributes, DisposableAttributes.AcquiresOwnershipAttribute)
                    ? OwnershipKind.Owned : OwnershipKind.Unspecified;
            if (source == OwnershipKind.Borrowed || i == 0 && source != OwnershipKind.Unspecified)
            {
                return source;
            }

            if (source != OwnershipKind.Unspecified)
            {
                inherited = source;
            }
        }

        if (inherited != OwnershipKind.Unspecified)
        {
            return inherited;
        }

        foreach (var member in members)
        {
            var parameters = GetParameters(member);
            if (ordinal >= parameters.Length)
            {
                continue;
            }

            foreach (var contract in ExternalContracts(member))
            {
                if (contract.Parameters.TryGetValue(parameters[ordinal].Name, out var ownership))
                {
                    return ownership;
                }
            }
        }

        return OwnershipKind.Unspecified;
    }

    private IEnumerable<ExternalContract> ExternalContracts(ISymbol member)
    {
        var id = member.GetDocumentationCommentId();
        if (id == null)
        {
            yield break;
        }

        if (_external.TryGetValue(Key(id, member.ContainingAssembly?.Name), out var specific))
        {
            yield return specific;
        }

        if (_external.TryGetValue(Key(id, null), out var general))
        {
            yield return general;
        }
    }

    private static ISymbol Normalize(ISymbol symbol)
    {
        if (symbol is IMethodSymbol method)
        {
            symbol = method.ReducedFrom ?? method;
            if (symbol is IMethodSymbol { AssociatedSymbol: IPropertySymbol property })
            {
                symbol = property;
            }
        }

        return symbol.OriginalDefinition;
    }

    private static IEnumerable<ISymbol> RelatedMembers(ISymbol symbol)
    {
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var chain = new List<ISymbol>();
        for (ISymbol? current = symbol; current != null; current = current switch
        {
            IMethodSymbol method => method.OverriddenMethod?.OriginalDefinition,
            IPropertySymbol property => property.OverriddenProperty?.OriginalDefinition,
            _ => null,
        })
        {
            chain.Add(current);
            if (seen.Add(current))
            {
                yield return current;
            }
        }

        if (symbol.ContainingType == null)
        {
            yield break;
        }

        foreach (var type in symbol.ContainingType.AllInterfaces)
        {
            foreach (var member in type.GetMembers())
            {
                if (member.Kind != symbol.Kind)
                {
                    continue;
                }

                var implementation = symbol.ContainingType.FindImplementationForInterfaceMember(member);
                if (implementation != null
                    && chain.Any(candidate => SymbolEqualityComparer.Default.Equals(
                        candidate, implementation.OriginalDefinition))
                    && seen.Add(member.OriginalDefinition))
                {
                    yield return member.OriginalDefinition;
                }
            }
        }
    }

    private static OwnershipKind ReturnAttributeOwnership(ISymbol member)
    {
        var attributes = member.GetAttributes();
        if (member is IMethodSymbol method)
        {
            attributes = attributes.AddRange(method.GetReturnTypeAttributes());
        }
        else if (member is IPropertySymbol { GetMethod: { } getter })
        {
            attributes = attributes.AddRange(getter.GetAttributes()).AddRange(getter.GetReturnTypeAttributes());
        }

        if (HasAttribute(attributes, DisposableAttributes.KeepsOwnershipAttribute)
            || HasAttribute(attributes, DisposableAttributes.DoNotDisposeAttribute)
            || member is IPropertySymbol && HasAttribute(attributes, DisposableAttributes.NoOwnershipAttribute))
        {
            return OwnershipKind.Borrowed;
        }

        return HasAttribute(attributes, DisposableAttributes.ReturnsOwnershipAttribute)
            ? OwnershipKind.Owned : OwnershipKind.Unspecified;
    }

    private static bool HasBorrowedAttribute(ImmutableArray<AttributeData> attributes) =>
        HasAttribute(attributes, DisposableAttributes.DoNotDisposeAttribute)
        || HasAttribute(attributes, DisposableAttributes.NoOwnershipAttribute);

    private static bool HasAttribute(ImmutableArray<AttributeData> attributes, string name) =>
        attributes.Any(attribute => attribute.AttributeClass?.Name == name);

    private static ImmutableArray<IParameterSymbol> GetParameters(ISymbol symbol) => symbol switch
    {
        IMethodSymbol method => method.Parameters,
        IPropertySymbol property => property.Parameters,
        _ => ImmutableArray<IParameterSymbol>.Empty,
    };

    private static OwnershipKind ParseOwnership(string? value) => value switch
    {
        "owned" => OwnershipKind.Owned,
        "borrowed" => OwnershipKind.Borrowed,
        _ => OwnershipKind.Unspecified,
    };

    private static bool ValidateShape(XElement element, string[] attributes,
        Action<string, XObject?, int, int> report)
    {
        foreach (var attribute in element.Attributes())
        {
            if (!attributes.Contains(attribute.Name.ToString(), StringComparer.Ordinal))
            {
                report($"Unknown attribute '{attribute.Name}'.", attribute, 1, 1);
                return false;
            }
        }

        if (element.Nodes().OfType<XText>().Any(node => !string.IsNullOrWhiteSpace(node.Value)))
        {
            report("Text content is not allowed in ownership annotations.", element, 1, 1);
            return false;
        }

        return true;
    }

    private static bool Conflicts(ExternalContract left, ExternalContract right) =>
        left.Returns != OwnershipKind.Unspecified && right.Returns != OwnershipKind.Unspecified
            && left.Returns != right.Returns
        || left.Parameters.Any(parameter => right.Parameters.TryGetValue(parameter.Key, out var ownership)
            && ownership != parameter.Value);

    private static string Key(string id, string? assembly) => (assembly ?? "") + "\0" + id;

    private sealed class ExternalContract
    {
        public ExternalContract(string id, string? assembly, OwnershipKind returns,
            ImmutableDictionary<string, OwnershipKind> parameters)
        {
            Id = id;
            Assembly = assembly;
            Returns = returns;
            Parameters = parameters;
        }

        public string Id { get; }
        public string? Assembly { get; }
        public OwnershipKind Returns { get; }
        public ImmutableDictionary<string, OwnershipKind> Parameters { get; }
    }

    // Binding alone cannot validate IDs for libraries absent from the compilation. This small parser
    // accepts declaration-ID names, generic arguments/parameters, arrays, pointers, refs and conversions.
    private sealed class DocumentationIdSyntax
    {
        private readonly string _text;
        private int _position = 2;

        private DocumentationIdSyntax(string text) => _text = text;

        public static bool IsValid(string text)
        {
            if (text.Length < 5 || (text[0] != 'M' && text[0] != 'P') || text[1] != ':')
            {
                return false;
            }

            var parser = new DocumentationIdSyntax(text);
            var start = parser._position;
            if (!parser.Name(0) || text.Substring(start, parser._position - start).IndexOf('.') < 0)
            {
                return false;
            }

            if (parser.Take('('))
            {
                if (!parser.Type(0))
                {
                    return false;
                }

                while (parser.Take(','))
                {
                    if (!parser.Type(0))
                    {
                        return false;
                    }
                }

                if (!parser.Take(')'))
                {
                    return false;
                }
            }

            if (parser.Take('~') && (text[0] != 'M' || !parser.Type(0)))
            {
                return false;
            }

            return parser._position == text.Length;
        }

        private bool Type(int depth)
        {
            if (depth > 32)
            {
                return false;
            }

            if (Take('`'))
            {
                Take('`');
                if (!Digits())
                {
                    return false;
                }
            }
            else if (!Name(depth))
            {
                return false;
            }

            while (_position < _text.Length)
            {
                if (Take('*'))
                {
                    continue;
                }

                if (!Take('['))
                {
                    break;
                }

                if (Take(']'))
                {
                    continue;
                }

                do
                {
                    Take('-');
                    Digits();
                    if (!Take(':'))
                    {
                        return false;
                    }

                    Take('-');
                    Digits();
                } while (Take(','));
                if (!Take(']'))
                {
                    return false;
                }
            }

            Take('@');
            return true;
        }

        private bool Name(int depth)
        {
            if (depth > 32)
            {
                return false;
            }

            do
            {
                // '#' also encodes explicit-interface separators and constructor names.
                Take('#');
                if (_position == _text.Length || !IdentifierStart(_text[_position]))
                {
                    return false;
                }

                _position++;
                while (_position < _text.Length &&
                    (IdentifierStart(_text[_position]) || char.IsDigit(_text[_position])))
                {
                    _position++;
                }

                if (Take('`'))
                {
                    Take('`');
                    if (_position == _text.Length || _text[_position] == '0' || !Digits())
                    {
                        return false;
                    }
                }

                if (Take('{'))
                {
                    if (!Type(depth + 1))
                    {
                        return false;
                    }

                    while (Take(','))
                    {
                        if (!Type(depth + 1))
                        {
                            return false;
                        }
                    }

                    if (!Take('}'))
                    {
                        return false;
                    }
                }
            } while (Take('.') || Take('#'));

            return true;
        }

        private bool Digits()
        {
            var start = _position;
            while (_position < _text.Length && _text[_position] >= '0' && _text[_position] <= '9')
            {
                _position++;
            }

            return _position != start;
        }

        private bool Take(char value)
        {
            if (_position == _text.Length || _text[_position] != value)
            {
                return false;
            }

            _position++;
            return true;
        }

        private static bool IdentifierStart(char value) => char.IsLetter(value) || value == '_';
    }
}

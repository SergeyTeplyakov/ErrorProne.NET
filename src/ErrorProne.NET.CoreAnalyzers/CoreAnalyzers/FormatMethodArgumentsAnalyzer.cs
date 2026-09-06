// --------------------------------------------------------------------
//  
// Copyright (c) Microsoft Corporation.  All rights reserved.
//  
// --------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// EPC41 — for methods marked as formatting methods via the
    /// <c>dotnet_diagnostic.EPC41.format_methods</c> <c>.editorconfig</c> option, validates that
    /// the format string passed at the configured parameter index references only argument indices
    /// that are actually supplied. Catches the same class of bug as CA2241 (which would throw
    /// <see cref="FormatException"/> at runtime) but for libraries the user does not own.
    /// </summary>
    /// <remarks>
    /// Configuration syntax:
    /// <code>
    /// dotnet_diagnostic.EPC41.format_methods =
    ///     MyCorp.Logging.Logger.LogInfo:0;
    ///     MyCorp.Logging.Logger.LogError:1
    /// </code>
    /// Each entry is <c>FullyQualifiedTypeName.MethodName:formatParamIndex</c>. The match is by
    /// name only — every overload of that name is treated as a formatting method.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class FormatMethodArgumentsAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptors.EPC41;

        /// <nodoc />
        public const string FormatMethodsOptionKey = "dotnet_diagnostic.epc41.format_methods";

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <nodoc />
        public FormatMethodArgumentsAnalyzer()
            : base(Rule)
        {
        }

        // Used to format a containing-type for matching against the editorconfig entries. Produces
        // e.g. "System.String" / "MyCorp.Logger" (no `global::`, no special keyword names like `string`).
        private static readonly SymbolDisplayFormat ContainingTypeFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var registry = new FormatMethodRegistry();

            context.RegisterOperationAction(operationContext =>
            {
                var invocation = (IInvocationOperation)operationContext.Operation;
                var syntaxTree = invocation.Syntax.SyntaxTree;
                var options = operationContext.Options.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree);

                if (!registry.TryGetFormatStringParamIndex(invocation.TargetMethod, options, out var formatParamIndex))
                {
                    return;
                }

                AnalyzeInvocation(operationContext, invocation, formatParamIndex);
            }, OperationKind.Invocation);
        }

        private static void AnalyzeInvocation(
            OperationAnalysisContext context,
            IInvocationOperation invocation,
            int formatParamIndex)
        {
            // invocation.Arguments is sorted by parameter ordinal, so we can index by it directly
            // whether the caller used positional or named arguments.
            if (formatParamIndex < 0 || formatParamIndex >= invocation.Arguments.Length)
            {
                return;
            }

            AnalyzeFormatString(context, invocation, invocation.Arguments[formatParamIndex]);
        }

        private static void AnalyzeFormatString(OperationAnalysisContext context, IInvocationOperation invocation, IArgumentOperation formatArgument)
        {
            var constant = formatArgument.Value.ConstantValue;
            if (!constant.HasValue || constant.Value is not string formatString)
            {
                // Not a compile-time constant string — we cannot reason about it.
                return;
            }

            if (formatArgument.Parameter is null
                || !TryCountTrailingArgs(invocation, formatArgument.Parameter.Ordinal, out var argCount))
            {
                // Caller passed e.g. an opaque object[] for the params slot; cannot reason statically.
                return;
            }

            var parseResult = FormatStringParser.Parse(formatString);
            if (parseResult.IsMalformed)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    formatArgument.Syntax.GetLocation(),
                    $"Format string is malformed near index {parseResult.MalformedAt}: '{Truncate(formatString, parseResult.MalformedAt)}'"));
                return;
            }

            if (parseResult.MaxIndex >= argCount)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    invocation.Syntax.GetLocation(),
                    $"Format placeholder {{{parseResult.MaxIndex}}} references argument index {parseResult.MaxIndex}, but only {argCount} argument(s) supplied; the call would throw FormatException at runtime."));
            }
        }

        private static bool TryCountTrailingArgs(
            IInvocationOperation invocation,
            int formatParamIndex,
            out int argCount)
        {
            argCount = 0;
            for (var i = formatParamIndex + 1; i < invocation.Arguments.Length; i++)
            {
                var arg = invocation.Arguments[i];
                var parameter = arg.Parameter;

                if (parameter is { IsParams: true })
                {
                    // Roslyn wraps individually-passed params args into an implicit array creation.
                    if (arg.Value is IArrayCreationOperation { IsImplicit: true, Initializer: { } init })
                    {
                        argCount += init.ElementValues.Length;
                        continue;
                    }

                    // Caller passed a single array expression directly. If it's an explicit array
                    // literal we can still count its elements; otherwise bail out.
                    if (arg.Value is IArrayCreationOperation { Initializer: { } explicitInit })
                    {
                        argCount += explicitInit.ElementValues.Length;
                        continue;
                    }

                    return false;
                }

                argCount++;
            }

            return true;
        }

        private static string Truncate(string s, int around)
        {
            const int window = 12;
            var start = Math.Max(0, around - window);
            var end = Math.Min(s.Length, around + window);
            return s.Substring(start, end - start);
        }

        /// <summary>
        /// Lazily parses <c>dotnet_diagnostic.EPC41.format_methods</c> values and caches the
        /// resulting entry table keyed by the raw editorconfig string.
        /// </summary>
        /// <remarks>
        /// Entry syntax: <c>FullyQualifiedType.MethodName[*]:(index|paramName)</c>. A trailing
        /// <c>*</c> on the method name turns the entry into a prefix match against the called
        /// method's name. The spec after <c>:</c> is either a zero-based positional index, or the
        /// name of the format-string parameter on the resolved method.
        /// </remarks>
        private sealed class FormatMethodRegistry
        {
            private readonly ConcurrentDictionary<string, ParsedEntries> _cache =
                new ConcurrentDictionary<string, ParsedEntries>(StringComparer.Ordinal);

            public bool TryGetFormatStringParamIndex(
                IMethodSymbol method,
                AnalyzerConfigOptions options,
                out int formatParamIndex)
            {
                formatParamIndex = -1;

                if (!options.TryGetValue(FormatMethodsOptionKey, out var raw) || string.IsNullOrWhiteSpace(raw))
                {
                    return false;
                }

                var entries = _cache.GetOrAdd(raw, Parse);
                if (entries.IsEmpty)
                {
                    return false;
                }

                var containing = method.ContainingType?.ToDisplayString(ContainingTypeFormat);
                if (containing == null)
                {
                    return false;
                }

                // Exact match takes precedence over wildcard match.
                var exactKey = containing + "." + method.Name;
                if (entries.Exact.TryGetValue(exactKey, out var spec)
                    && TryResolveSpec(method, spec, out formatParamIndex))
                {
                    return true;
                }

                foreach (var wildcard in entries.Wildcards)
                {
                    if (!string.Equals(wildcard.Containing, containing, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!method.Name.StartsWith(wildcard.Prefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (TryResolveSpec(method, wildcard.Spec, out formatParamIndex))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool TryResolveSpec(IMethodSymbol method, string spec, out int formatParamIndex)
            {
                if (int.TryParse(spec, out formatParamIndex))
                {
                    return formatParamIndex >= 0 && formatParamIndex < method.Parameters.Length;
                }

                for (var i = 0; i < method.Parameters.Length; i++)
                {
                    if (string.Equals(method.Parameters[i].Name, spec, StringComparison.Ordinal))
                    {
                        formatParamIndex = i;
                        return true;
                    }
                }

                formatParamIndex = -1;
                return false;
            }

            private static ParsedEntries Parse(string raw)
            {
                var exact = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
                var wildcards = ImmutableArray.CreateBuilder<(string Containing, string Prefix, string Spec)>();

                foreach (var rawEntry in raw.Split(EntrySeparators, StringSplitOptions.RemoveEmptyEntries))
                {
                    var entry = rawEntry.Trim();
                    if (entry.Length == 0)
                    {
                        continue;
                    }

                    var colon = entry.LastIndexOf(':');
                    if (colon <= 0 || colon == entry.Length - 1)
                    {
                        continue;
                    }

                    var name = entry.Substring(0, colon).Trim();
                    var spec = entry.Substring(colon + 1).Trim();
                    if (name.Length == 0 || spec.Length == 0)
                    {
                        continue;
                    }

                    if (name.EndsWith("*", StringComparison.Ordinal))
                    {
                        var withoutStar = name.Substring(0, name.Length - 1);
                        var dot = withoutStar.LastIndexOf('.');
                        if (dot <= 0 || dot == withoutStar.Length - 1)
                        {
                            // Wildcard without a method-name segment (e.g. "Foo.*") — skip.
                            continue;
                        }

                        var containing = withoutStar.Substring(0, dot);
                        var prefix = withoutStar.Substring(dot + 1);
                        wildcards.Add((containing, prefix, spec));
                    }
                    else
                    {
                        exact[name] = spec;
                    }
                }

                return new ParsedEntries(exact.ToImmutable(), wildcards.ToImmutable());
            }

            private static readonly char[] EntrySeparators = { ',', ';', '\r', '\n' };

            private readonly struct ParsedEntries
            {
                public ParsedEntries(
                    ImmutableDictionary<string, string> exact,
                    ImmutableArray<(string Containing, string Prefix, string Spec)> wildcards)
                {
                    Exact = exact;
                    Wildcards = wildcards;
                }

                public ImmutableDictionary<string, string> Exact { get; }
                public ImmutableArray<(string Containing, string Prefix, string Spec)> Wildcards { get; }
                public bool IsEmpty => Exact.Count == 0 && Wildcards.Length == 0;
            }
        }

        /// <summary>
        /// Minimal <see cref="string.Format(string, object)"/>-compatible format string parser.
        /// Returns the maximum referenced placeholder index (-1 if none) and a flag indicating
        /// whether the string is malformed.
        /// </summary>
        private static class FormatStringParser
        {
            public readonly struct Result
            {
                public Result(int maxIndex, bool isMalformed, int malformedAt)
                {
                    MaxIndex = maxIndex;
                    IsMalformed = isMalformed;
                    MalformedAt = malformedAt;
                }

                public int MaxIndex { get; }
                public bool IsMalformed { get; }
                public int MalformedAt { get; }
            }

            public static Result Parse(string format)
            {
                var maxIndex = -1;
                var i = 0;
                while (i < format.Length)
                {
                    var c = format[i];
                    if (c == '{')
                    {
                        if (i + 1 < format.Length && format[i + 1] == '{')
                        {
                            i += 2;
                            continue;
                        }

                        // Read placeholder: {N[,alignment][:format]}
                        i++;
                        if (i >= format.Length || !IsAsciiDigit(format[i]))
                        {
                            return new Result(maxIndex, true, i);
                        }

                        var n = 0;
                        while (i < format.Length && IsAsciiDigit(format[i]))
                        {
                            n = n * 10 + (format[i] - '0');
                            if (n > 1_000_000)
                            {
                                // Way too large; treat as malformed to avoid pathological inputs.
                                return new Result(maxIndex, true, i);
                            }
                            i++;
                        }

                        if (n > maxIndex)
                        {
                            maxIndex = n;
                        }

                        // Optional alignment ',-?digits' and optional format spec ':...'. Skip to '}'.
                        var sawClose = false;
                        while (i < format.Length)
                        {
                            var d = format[i];
                            if (d == '}')
                            {
                                sawClose = true;
                                i++;
                                break;
                            }

                            if (d == '{')
                            {
                                // Nested unescaped '{' inside placeholder: malformed.
                                return new Result(maxIndex, true, i);
                            }

                            i++;
                        }

                        if (!sawClose)
                        {
                            return new Result(maxIndex, true, format.Length);
                        }

                        continue;
                    }

                    if (c == '}')
                    {
                        if (i + 1 < format.Length && format[i + 1] == '}')
                        {
                            i += 2;
                            continue;
                        }

                        return new Result(maxIndex, true, i);
                    }

                    i++;
                }

                return new Result(maxIndex, false, -1);
            }

            private static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';
        }
    }
}

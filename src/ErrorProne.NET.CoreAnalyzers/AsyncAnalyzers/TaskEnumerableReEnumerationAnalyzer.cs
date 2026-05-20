// --------------------------------------------------------------------
//  
// Copyright (c) Microsoft Corporation.  All rights reserved.
//  
// --------------------------------------------------------------------

using System.Collections.Generic;
using System.Collections.Immutable;
using ErrorProne.NET.Core;
using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.AsyncAnalyzers
{
    /// <summary>
    /// EPC38 — detects when a deferred <c>IEnumerable&lt;Task&gt;</c> / <c>IEnumerable&lt;Task&lt;T&gt;&gt;</c>
    /// is observed (enumerated) more than once inside the same method body.
    ///
    /// <para>
    /// Every enumeration of such a sequence executes the underlying producer again — e.g.
    /// <c>tasks.Select(async x =&gt; ...)</c> creates a fresh task per element on each pass. Patterns like
    /// <c>await Task.WhenAll(tasks); foreach (var t in tasks) { ... }</c> silently restart the work and
    /// observe a different set of tasks the second time, which is almost always a bug.
    /// </para>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class TaskEnumerableReEnumerationAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptors.EPC38;

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <nodoc />
        public TaskEnumerableReEnumerationAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterOperationBlockAction(AnalyzeOperationBlock);
        }

        private static void AnalyzeOperationBlock(OperationBlockAnalysisContext context)
        {
            // Per-block tracking: for each tracked symbol record its assignment epoch and enumeration sites
            // in syntactic order, then report any non-first enumeration within the same epoch.
            //
            // We treat lambdas / local functions as opaque (their bodies have their own block events).
            var trackers = new Dictionary<ISymbol, SymbolTracker>(SymbolEqualityComparer.Default);

            foreach (var block in context.OperationBlocks)
            {
                CollectSites(block, trackers, context);
            }

            ReportSites(trackers, context);
        }

        private static void CollectSites(
            IOperation root,
            Dictionary<ISymbol, SymbolTracker> trackers,
            OperationBlockAnalysisContext context)
        {
            foreach (var op in root.EnumerateChildOperations())
            {
                // Don't descend into nested lambdas / local functions — they'll get their own block event.
                if (IsInsideNestedFunction(op, root))
                {
                    continue;
                }

                TryRecordAssignment(op, trackers, context.Compilation);
                TryRecordEnumeration(op, trackers, context.Compilation);
            }
        }

        private static bool IsInsideNestedFunction(IOperation op, IOperation root)
        {
            // An operation that has a different containing local function / lambda than the block root
            // is part of a nested function and will be analyzed by its own block-action invocation.
            for (var current = op.Parent; current is not null && current != root; current = current.Parent)
            {
                if (current is IAnonymousFunctionOperation || current is ILocalFunctionOperation)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Records that a tracked symbol gained a *new* value, ending its previous "enumeration epoch".
        /// Aliasing (<c>var y = x;</c>) is also tracked: if the right-hand side is a reference to another
        /// tracked symbol, the two share the same epoch.
        /// </summary>
        private static void TryRecordAssignment(
            IOperation op,
            Dictionary<ISymbol, SymbolTracker> trackers,
            Compilation compilation)
        {
            switch (op)
            {
                case IVariableDeclaratorOperation declarator:
                {
                    var symbol = declarator.Symbol;
                    if (!IsTrackable(symbol.Type, compilation))
                    {
                        return;
                    }

                    var initializer = declarator.Initializer?.Value
                                      ?? (declarator.Parent as IVariableDeclarationOperation)?.Initializer?.Value;

                    var tracker = GetOrCreate(trackers, symbol);
                    var aliasRoot = EnumerationMethods.TryGetRootEnumerableSymbol(initializer, compilation);
                    if (aliasRoot is not null
                        && !SymbolEqualityComparer.Default.Equals(aliasRoot, symbol)
                        && IsTrackable(GetSymbolType(aliasRoot), compilation))
                    {
                        // Right-hand side is itself a trackable enumerable (a parameter or another local).
                        // Make the two share a tracker so that subsequent enumerations on either name accrue
                        // toward the same set of sites.
                        var aliasTarget = GetOrCreate(trackers, aliasRoot);
                        tracker.AliasTo(aliasTarget);
                    }
                    else
                    {
                        tracker.NewEpoch(op.Syntax.SpanStart);
                    }

                    return;
                }

                case ISimpleAssignmentOperation assignment:
                {
                    var rootSymbol = EnumerationMethods.TryGetRootEnumerableSymbol(assignment.Target, compilation);
                    if (rootSymbol is null || !IsTrackable(GetSymbolType(rootSymbol), compilation))
                    {
                        return;
                    }

                    var tracker = GetOrCreate(trackers, rootSymbol);
                    tracker.NewEpoch(op.Syntax.SpanStart);
                    return;
                }
            }
        }

        /// <summary>
        /// Records that an enumeration of a tracked symbol happens at the given operation site.
        /// </summary>
        private static void TryRecordEnumeration(
            IOperation op,
            Dictionary<ISymbol, SymbolTracker> trackers,
            Compilation compilation)
        {
            switch (op)
            {
                case IForEachLoopOperation foreachLoop:
                    RecordIfTracked(
                        foreachLoop.Collection,
                        trackers,
                        compilation,
                        foreachLoop.Collection.Syntax.GetLocation());
                    return;

                case IInvocationOperation invocation:
                {
                    var target = invocation.TargetMethod;

                    // Task aggregation methods: Task.WhenAll(IEnumerable<Task>) and friends. The first
                    // positional argument is the enumerable being observed.
                    if (EnumerationMethods.IsTaskAggregationMethod(target, compilation))
                    {
                        if (invocation.Arguments.Length > 0)
                        {
                            var argValue = invocation.Arguments[0].Value;
                            RecordIfTracked(argValue, trackers, compilation, argValue.Syntax.GetLocation());
                        }

                        return;
                    }

                    // LINQ enumerating methods (Count, ToList, Sum, …) — receiver is the source.
                    if (EnumerationMethods.IsEnumeratingLinqMethod(target, compilation))
                    {
                        // For instance / reduced-extension calls the source is the receiver.
                        var source = invocation.Instance ?? (invocation.Arguments.Length > 0 ? invocation.Arguments[0].Value : null);
                        if (source is not null)
                        {
                            RecordIfTracked(source, trackers, compilation, source.Syntax.GetLocation());
                        }

                        return;
                    }

                    return;
                }
            }
        }

        private static void RecordIfTracked(
            IOperation? sourceOperation,
            Dictionary<ISymbol, SymbolTracker> trackers,
            Compilation compilation,
            Location reportLocation)
        {
            if (sourceOperation is null)
            {
                return;
            }

            var rootSymbol = EnumerationMethods.TryGetRootEnumerableSymbol(sourceOperation, compilation);
            if (rootSymbol is null)
            {
                return;
            }

            var symbolType = GetSymbolType(rootSymbol);
            if (!IsTrackable(symbolType, compilation))
            {
                return;
            }

            var tracker = GetOrCreate(trackers, rootSymbol);
            tracker.AddEnumeration(sourceOperation.Syntax.SpanStart, reportLocation, rootSymbol.Name);
        }

        private static void ReportSites(
            Dictionary<ISymbol, SymbolTracker> trackers,
            OperationBlockAnalysisContext context)
        {
            // Each tracker is a separate logical sequence (post-alias resolution we use the *root* of the
            // alias chain so multiple aliases collapse into one). We report every enumeration after the first
            // in any epoch that has more than one site.
            var roots = new HashSet<SymbolTracker>();
            foreach (var tracker in trackers.Values)
            {
                roots.Add(tracker.Resolve());
            }

            foreach (var root in roots)
            {
                root.ReportRepeatedSites(context, Rule);
            }
        }

        private static bool IsTrackable(ITypeSymbol? type, Compilation compilation)
        {
            return EnumerationMethods.IsTaskEnumerableType(type, compilation);
        }

        private static ITypeSymbol? GetSymbolType(ISymbol symbol)
        {
            return symbol switch
            {
                ILocalSymbol l => l.Type,
                IParameterSymbol p => p.Type,
                _ => null,
            };
        }

        private static SymbolTracker GetOrCreate(Dictionary<ISymbol, SymbolTracker> trackers, ISymbol symbol)
        {
            if (!trackers.TryGetValue(symbol, out var tracker))
            {
                tracker = new SymbolTracker(symbol);
                trackers[symbol] = tracker;
            }

            return tracker;
        }

        /// <summary>
        /// Per-symbol enumeration history. Records all assignment positions and all enumeration sites
        /// (regardless of visit order), then groups sites into "epochs" delimited by assignment positions
        /// at report time. Multiple aliased symbols share a single underlying tracker via
        /// <see cref="AliasTo"/> / <see cref="Resolve"/>.
        /// </summary>
        private sealed class SymbolTracker
        {
            private readonly ISymbol _symbol;
            private SymbolTracker? _aliasTarget;

            private readonly List<int> _assignmentPositions = new();
            private readonly List<Site> _sites = new();

            public SymbolTracker(ISymbol symbol) => _symbol = symbol;

            public SymbolTracker Resolve()
            {
                var current = this;
                while (current._aliasTarget is not null)
                {
                    current = current._aliasTarget;
                }

                return current;
            }

            public void AliasTo(SymbolTracker target)
            {
                var resolvedTarget = target.Resolve();
                if (ReferenceEquals(resolvedTarget, this))
                {
                    return;
                }

                // Migrate any sites/assignments that were recorded on this tracker (e.g. discovered before
                // the alias relationship was established) onto the target tracker.
                resolvedTarget._sites.AddRange(_sites);
                resolvedTarget._assignmentPositions.AddRange(_assignmentPositions);
                _sites.Clear();
                _assignmentPositions.Clear();

                _aliasTarget = resolvedTarget;
            }

            public void NewEpoch(int positionStart)
            {
                Resolve()._assignmentPositions.Add(positionStart);
            }

            public void AddEnumeration(int positionStart, Location location, string symbolName)
            {
                Resolve()._sites.Add(new Site(positionStart, location, symbolName));
            }

            public void ReportRepeatedSites(OperationBlockAnalysisContext context, DiagnosticDescriptor rule)
            {
                if (_sites.Count < 2)
                {
                    return;
                }

                _sites.Sort(static (a, b) => a.Position.CompareTo(b.Position));
                _assignmentPositions.Sort();

                // Group sites by epoch (= run between consecutive assignment positions).
                var epochStart = 0;
                while (epochStart < _sites.Count)
                {
                    var epochEnd = FindEpochEnd(epochStart);
                    if (epochEnd - epochStart >= 2)
                    {
                        // Report every site in this epoch after the first one.
                        for (var i = epochStart + 1; i < epochEnd; i++)
                        {
                            var site = _sites[i];
                            context.ReportDiagnostic(Diagnostic.Create(rule, site.Location, site.SymbolName));
                        }
                    }

                    epochStart = epochEnd;
                }
            }

            private int FindEpochEnd(int epochStart)
            {
                // The epoch containing _sites[epochStart] runs up to (but not including) the first site whose
                // position is on the other side of an assignment with respect to _sites[epochStart].
                var startPos = _sites[epochStart].Position;
                var firstBoundary = -1;
                foreach (var assignment in _assignmentPositions)
                {
                    if (assignment > startPos)
                    {
                        firstBoundary = assignment;
                        break;
                    }
                }

                if (firstBoundary < 0)
                {
                    return _sites.Count;
                }

                for (var i = epochStart + 1; i < _sites.Count; i++)
                {
                    if (_sites[i].Position >= firstBoundary)
                    {
                        return i;
                    }
                }

                return _sites.Count;
            }

            private readonly struct Site
            {
                public int Position { get; }
                public Location Location { get; }
                public string SymbolName { get; }

                public Site(int position, Location location, string symbolName)
                {
                    Position = position;
                    Location = location;
                    SymbolName = symbolName;
                }
            }
        }
    }
}

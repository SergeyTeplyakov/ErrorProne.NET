// --------------------------------------------------------------------
//  
// Copyright (c) Microsoft Corporation.  All rights reserved.
//  
// --------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using ErrorProne.NET.Core;
using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// EPC40 — for <c>private</c> methods that enumerate an <see cref="IEnumerable{T}"/> parameter
    /// more than once, cross-references all call sites (capped at <see cref="MaxCallers"/>) and reports
    /// any caller that passes a deferred LINQ query, <see cref="System.Linq.Enumerable.Range(int, int)"/>/
    /// <see cref="System.Linq.Enumerable.Repeat{TResult}(TResult, int)"/>/
    /// <see cref="System.Linq.Enumerable.Empty{TResult}"/>, or an iterator method.
    ///
    /// <para>
    /// Restricted to <c>private</c> methods because callers form a closed world within the compilation,
    /// making the analysis decidable without dataflow. Provenance is followed back at most
    /// <see cref="MaxProvenanceHops"/> hops through local declarations in the caller's body.
    /// </para>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class PrivateMethodMultipleEnumerationAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptors.EPC40;

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <nodoc />
        public PrivateMethodMultipleEnumerationAnalyzer()
            : base(Rule)
        {
        }

        /// <summary>Maximum number of call sites a candidate may have before we skip it entirely.</summary>
        private const int MaxCallers = 3;

        /// <summary>Maximum number of provenance hops to follow back through local declarations.</summary>
        private const int MaxProvenanceHops = 3;

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var state = new CompilationState();

            // Pass 1 — identify candidate (method, parameterOrdinal) pairs: private methods whose
            // IEnumerable<T> parameter is enumerated >= 2 times in the body.
            context.RegisterOperationBlockAction(blockContext =>
            {
                if (blockContext.OwningSymbol is not IMethodSymbol method)
                {
                    return;
                }

                if (method.DeclaredAccessibility != Accessibility.Private)
                {
                    return;
                }

                // Skip extern / abstract / partial-no-body methods — they have nothing to inspect.
                if (method.IsAbstract || method.IsExtern)
                {
                    return;
                }

                foreach (var parameter in method.Parameters)
                {
                    if (!EnumerationMethods.IsDeferredEnumerableType(parameter.Type))
                    {
                        continue;
                    }

                    foreach (var block in blockContext.OperationBlocks)
                    {
                        if (CountEnumerationsOfParameter(block, parameter, blockContext.Compilation) >= 2)
                        {
                            state.AddCandidate(method.OriginalDefinition, parameter.Ordinal);
                            break;
                        }
                    }
                }
            });

            // Pass 2 — record every invocation that targets a private method and passes an argument to a
            // deferred-IEnumerable parameter. We over-collect cheaply here and filter at the end.
            context.RegisterOperationAction(opContext =>
            {
                var invocation = (IInvocationOperation)opContext.Operation;
                var target = invocation.TargetMethod.OriginalDefinition;

                if (target.DeclaredAccessibility != Accessibility.Private)
                {
                    return;
                }

                foreach (var arg in invocation.Arguments)
                {
                    if (arg.ArgumentKind == ArgumentKind.DefaultValue)
                    {
                        // Caller omitted this argument — there is no syntactic source to report on.
                        continue;
                    }

                    var parameter = arg.Parameter;
                    if (parameter is null || !EnumerationMethods.IsDeferredEnumerableType(parameter.Type))
                    {
                        continue;
                    }

                    state.AddCallSite(target, parameter.Ordinal, arg);
                }
            }, OperationKind.Invocation);

            context.RegisterCompilationEndAction(endContext =>
            {
                state.Evaluate(endContext, MaxCallers, MaxProvenanceHops);
            });
        }

        /// <summary>
        /// Counts how many syntactic enumeration sites of <paramref name="parameter"/> appear in
        /// <paramref name="body"/>. Stops at 2 (the rule only cares whether the count is &gt;= 2).
        /// </summary>
        private static int CountEnumerationsOfParameter(IOperation body, IParameterSymbol parameter, Compilation compilation)
        {
            var count = 0;
            foreach (var op in body.EnumerateChildOperations())
            {
                if (IsEnumerationSiteOf(op, parameter, compilation))
                {
                    if (++count >= 2)
                    {
                        return count;
                    }
                }
            }

            return count;
        }

        private static bool IsEnumerationSiteOf(IOperation op, IParameterSymbol parameter, Compilation compilation)
        {
            switch (op)
            {
                case IForEachLoopOperation foreachOp:
                {
                    var root = EnumerationMethods.TryGetRootEnumerableSymbol(foreachOp.Collection, compilation);
                    return SymbolEqualityComparer.Default.Equals(root, parameter);
                }

                case IInvocationOperation invocation:
                {
                    var target = invocation.TargetMethod;
                    IOperation? source = null;

                    if (EnumerationMethods.IsEnumeratingLinqMethod(target, compilation))
                    {
                        source = invocation.Instance ??
                                 (invocation.Arguments.Length > 0 ? invocation.Arguments[0].Value : null);
                    }
                    else if (EnumerationMethods.IsTaskAggregationMethod(target, compilation))
                    {
                        source = invocation.Arguments.Length > 0 ? invocation.Arguments[0].Value : null;
                    }

                    if (source is null)
                    {
                        return false;
                    }

                    var root = EnumerationMethods.TryGetRootEnumerableSymbol(source, compilation);
                    return SymbolEqualityComparer.Default.Equals(root, parameter);
                }

                default:
                    return false;
            }
        }

        /// <summary>
        /// Shared, concurrency-safe state collected across the compilation.
        /// </summary>
        private sealed class CompilationState
        {
            // (methodOriginalDefinition, parameterOrdinal) — set of candidate "problematic" parameters.
            private readonly ConcurrentDictionary<CandidateKey, byte> _candidates =
                new ConcurrentDictionary<CandidateKey, byte>(CandidateKey.Comparer);

            // (methodOriginalDefinition, parameterOrdinal) — list of every recorded call-site argument.
            private readonly ConcurrentDictionary<CandidateKey, ConcurrentBag<IArgumentOperation>> _callSites =
                new ConcurrentDictionary<CandidateKey, ConcurrentBag<IArgumentOperation>>(CandidateKey.Comparer);

            // Per-method "is this an iterator method (has `yield` in its body)?" cache.
            private readonly ConcurrentDictionary<ISymbol, bool> _iteratorCache =
                new ConcurrentDictionary<ISymbol, bool>(SymbolEqualityComparer.Default);

            public void AddCandidate(IMethodSymbol method, int parameterOrdinal)
            {
                _candidates.TryAdd(new CandidateKey(method, parameterOrdinal), 0);
            }

            public void AddCallSite(IMethodSymbol method, int parameterOrdinal, IArgumentOperation argument)
            {
                var bag = _callSites.GetOrAdd(new CandidateKey(method, parameterOrdinal),
                    _ => new ConcurrentBag<IArgumentOperation>());
                bag.Add(argument);
            }

            public void Evaluate(CompilationAnalysisContext context, int maxCallers, int maxProvenanceHops)
            {
                foreach (var entry in _candidates)
                {
                    if (!_callSites.TryGetValue(entry.Key, out var bag))
                    {
                        continue;
                    }

                    var args = bag.ToArray();
                    if (args.Length == 0 || args.Length > maxCallers)
                    {
                        continue;
                    }

                    var method = entry.Key.Method;
                    var parameter = method.Parameters[entry.Key.ParameterOrdinal];

                    foreach (var arg in args)
                    {
                        var producerDescription = ClassifyProducer(arg, maxProvenanceHops, context.Compilation, _iteratorCache, context.CancellationToken);
                        if (producerDescription is null)
                        {
                            continue;
                        }

                        context.ReportDiagnostic(Diagnostic.Create(
                            Rule,
                            arg.Value.Syntax.GetLocation(),
                            method.Name,
                            parameter.Name,
                            producerDescription));
                    }
                }
            }
        }

        /// <summary>
        /// Walks back from <paramref name="arg"/> through up to <paramref name="maxHops"/> local
        /// declarations and returns a short human-readable description of the producing expression if it
        /// is a deferred query (LINQ chain, <c>Enumerable.Range/Repeat/Empty</c>, or an iterator method).
        /// Returns <see langword="null"/> when the producer cannot be classified as a deferred query.
        /// </summary>
        private static string? ClassifyProducer(
            IArgumentOperation arg,
            int maxHops,
            Compilation compilation,
            ConcurrentDictionary<ISymbol, bool> iteratorCache,
            CancellationToken cancellationToken)
        {
            var enclosingBody = GetEnclosingBody(arg);
            IOperation current = arg.Value;

            for (var hop = 0; hop <= maxHops; hop++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                current = Unwrap(current);

                switch (current)
                {
                    case IInvocationOperation invocation:
                    {
                        var target = invocation.TargetMethod;
                        if (EnumerationMethods.IsLinqChainMethod(target, compilation))
                        {
                            return $".{target.Name}(...)";
                        }

                        if (EnumerationMethods.IsEnumerableProducerMethod(target, compilation))
                        {
                            return $"Enumerable.{target.Name}(...)";
                        }

                        if (IsIteratorMethod(target, iteratorCache, cancellationToken))
                        {
                            return $"{target.Name}() (iterator)";
                        }

                        return null;
                    }

                    case ILocalReferenceOperation localRef when enclosingBody is not null:
                    {
                        var init = FindLocalInitializer(localRef.Local, enclosingBody);
                        if (init is null)
                        {
                            return null;
                        }

                        current = init;
                        continue;
                    }

                    default:
                        return null;
                }
            }

            return null;
        }

        private static IOperation Unwrap(IOperation op)
        {
            while (true)
            {
                switch (op)
                {
                    case IConversionOperation conv:
                        op = conv.Operand;
                        continue;
                    case IParenthesizedOperation paren:
                        op = paren.Operand;
                        continue;
                    default:
                        return op;
                }
            }
        }

        private static IOperation? GetEnclosingBody(IOperation op)
        {
            for (var cur = op.Parent; cur is not null; cur = cur.Parent)
            {
                if (cur is IMethodBodyOperation)
                {
                    return cur;
                }

                if (cur is IBlockOperation block && block.Parent is null)
                {
                    return block;
                }

                if (cur is IAnonymousFunctionOperation func)
                {
                    return func.Body;
                }

                if (cur is ILocalFunctionOperation localFunc)
                {
                    return localFunc.Body;
                }
            }

            return null;
        }

        private static IOperation? FindLocalInitializer(ILocalSymbol local, IOperation body)
        {
            foreach (var op in body.EnumerateChildOperations())
            {
                if (op is IVariableDeclaratorOperation declarator
                    && SymbolEqualityComparer.Default.Equals(declarator.Symbol, local))
                {
                    return declarator.Initializer?.Value
                        ?? (declarator.Parent as IVariableDeclarationOperation)?.Initializer?.Value;
                }
            }

            return null;
        }

        private static bool IsIteratorMethod(
            IMethodSymbol method,
            ConcurrentDictionary<ISymbol, bool> cache,
            CancellationToken cancellationToken)
        {
            return cache.GetOrAdd(method.OriginalDefinition, m => ComputeIsIteratorMethod((IMethodSymbol)m, cancellationToken));
        }

        private static bool ComputeIsIteratorMethod(IMethodSymbol method, CancellationToken cancellationToken)
        {
            foreach (var syntaxRef in method.DeclaringSyntaxReferences)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var syntax = syntaxRef.GetSyntax(cancellationToken);
                var body = ExtractBody(syntax);
                if (body is null)
                {
                    continue;
                }

                foreach (var descendant in body.DescendantNodes(static node =>
                    node is not LocalFunctionStatementSyntax &&
                    node is not LambdaExpressionSyntax &&
                    node is not AnonymousMethodExpressionSyntax))
                {
                    if (descendant is YieldStatementSyntax)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static SyntaxNode? ExtractBody(SyntaxNode syntax)
        {
            return syntax switch
            {
                MethodDeclarationSyntax md => (SyntaxNode?)md.Body ?? md.ExpressionBody,
                LocalFunctionStatementSyntax lfs => (SyntaxNode?)lfs.Body ?? lfs.ExpressionBody,
                AccessorDeclarationSyntax acc => (SyntaxNode?)acc.Body ?? acc.ExpressionBody,
                _ => null,
            };
        }

        /// <summary>
        /// Composite key for (method original-definition, parameter ordinal). Equality is based on
        /// <see cref="SymbolEqualityComparer.Default"/> so the same method symbol coming from different
        /// declaration sites compares equal.
        /// </summary>
        private readonly struct CandidateKey : IEquatable<CandidateKey>
        {
            public static readonly IEqualityComparer<CandidateKey> Comparer = new CandidateKeyComparer();

            public IMethodSymbol Method { get; }
            public int ParameterOrdinal { get; }

            public CandidateKey(IMethodSymbol method, int parameterOrdinal)
            {
                Method = method;
                ParameterOrdinal = parameterOrdinal;
            }

            public bool Equals(CandidateKey other) =>
                SymbolEqualityComparer.Default.Equals(Method, other.Method) &&
                ParameterOrdinal == other.ParameterOrdinal;

            public override bool Equals(object? obj) => obj is CandidateKey k && Equals(k);

            public override int GetHashCode() =>
                unchecked(SymbolEqualityComparer.Default.GetHashCode(Method) * 397 ^ ParameterOrdinal);

            private sealed class CandidateKeyComparer : IEqualityComparer<CandidateKey>
            {
                public bool Equals(CandidateKey x, CandidateKey y) => x.Equals(y);
                public int GetHashCode(CandidateKey obj) => obj.GetHashCode();
            }
        }
    }
}

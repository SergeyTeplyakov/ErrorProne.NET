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

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// EPC39 — detects three patterns where the *same* enumerable is iterated more than once inside a loop,
    /// producing O(N²) (or worse) behavior:
    /// <list type="number">
    /// <item><description>Q1 — same-symbol nested enumeration: a LINQ-enumerating call whose source is the
    /// same local/parameter that is also the foreach source.</description></item>
    /// <item><description>Q2 — re-enumeration of a deferred <see cref="System.Collections.Generic.IEnumerable{T}"/>
    /// inside a loop (the receiver's static type is <c>IEnumerable&lt;T&gt;</c>).</description></item>
    /// <item><description>Q4 — nested <c>foreach</c> over the same source.</description></item>
    /// </list>
    /// <para>
    /// Generic "O(N) method called inside a loop" patterns (e.g. <c>List&lt;T&gt;.Contains</c> /
    /// <c>Array.IndexOf</c> on a different collection than the loop source) are intentionally NOT flagged:
    /// they are often correct on small inputs and produced too much noise in real codebases.
    /// </para>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class QuadraticEnumerationAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptors.EPC39;

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <nodoc />
        public QuadraticEnumerationAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterOperationAction(AnalyzeLoop, OperationKind.Loop);
        }

        private static void AnalyzeLoop(OperationAnalysisContext context)
        {
            var loop = (ILoopOperation)context.Operation;

            // Resolve the loop's source symbol if it's a foreach over a local/parameter (drives Q1 and Q4).
            ISymbol? loopSource = null;
            if (loop is IForEachLoopOperation foreachLoop)
            {
                loopSource = EnumerationMethods.TryGetRootEnumerableSymbol(foreachLoop.Collection, context.Compilation);
            }

            var reported = new HashSet<SyntaxNode>();

            foreach (var op in loop.Body.EnumerateChildOperations())
            {
                if (IsInsideNestedFunction(op, loop.Body))
                {
                    continue;
                }

                // Skip any operation that lives inside an inner loop — the inner loop will analyze
                // its own body on its own context, and reporting at both levels causes noise.
                if (IsInsideInnerLoop(op, loop))
                {
                    continue;
                }

                switch (op)
                {
                    case IForEachLoopOperation inner when loopSource is not null:
                    {
                        // Q4 — nested foreach over the same source as the outer foreach.
                        var innerSource = EnumerationMethods.TryGetRootEnumerableSymbol(inner.Collection, context.Compilation);
                        if (innerSource is not null && SymbolEqualityComparer.Default.Equals(innerSource, loopSource))
                        {
                            Report(context, reported, inner.Collection.Syntax,
                                $"nested foreach iterates over the same source '{loopSource.Name}' twice (O(N²)).");
                        }

                        break;
                    }

                    case IInvocationOperation invocation:
                        AnalyzeInvocation(context, loop, loopSource, invocation, reported);
                        break;
                }
            }
        }

        private static void AnalyzeInvocation(
            OperationAnalysisContext context,
            ILoopOperation loop,
            ISymbol? loopSource,
            IInvocationOperation invocation,
            HashSet<SyntaxNode> reported)
        {
            var compilation = context.Compilation;
            var target = invocation.TargetMethod;

            // ------- Q1 & Q2: enumerating LINQ method called on some source ----------------------------
            if (EnumerationMethods.IsEnumeratingLinqMethod(target, compilation))
            {
                var sourceOp = invocation.Instance
                               ?? (invocation.Arguments.Length > 0 ? invocation.Arguments[0].Value : null);
                if (sourceOp is null)
                {
                    return;
                }

                var rootSymbol = EnumerationMethods.TryGetRootEnumerableSymbol(sourceOp, compilation);
                if (rootSymbol is null)
                {
                    return;
                }

                // Q1: same symbol as the outer foreach's collection.
                if (loopSource is not null && SymbolEqualityComparer.Default.Equals(rootSymbol, loopSource))
                {
                    Report(context, reported, invocation.Syntax,
                        $"LINQ method '{target.Name}' is called on the same enumerable '{rootSymbol.Name}' that is being iterated (O(N²)).");
                    return;
                }

                // Q2: source is a deferred IEnumerable<T> local/parameter.
                var symbolType = GetSymbolType(rootSymbol);
                if (EnumerationMethods.IsDeferredEnumerableType(symbolType))
                {
                    // If the source local is declared inside the loop body, a fresh enumerable is bound
                    // each iteration — the cost does NOT compound with the outer loop size. Multiple
                    // enumerations within a single iteration are a constant-factor multiple-enumeration
                    // concern (see CA1851), not the quadratic-in-the-loop pattern EPC39 targets. Skip.
                    if (IsLocalDeclaredInsideLoopBody(rootSymbol, loop))
                    {
                        return;
                    }

                    Report(context, reported, invocation.Syntax,
                        $"LINQ method '{target.Name}' re-enumerates deferred IEnumerable '{rootSymbol.Name}' on every iteration; materialize it with .ToList()/.ToArray() before the loop.");
                    return;
                }
            }
        }

        private static bool IsLocalDeclaredInsideLoopBody(ISymbol symbol, ILoopOperation loop)
        {
            if (symbol is not ILocalSymbol local)
            {
                return false;
            }

            var bodySyntax = loop.Body.Syntax;
            var bodyTree = bodySyntax.SyntaxTree;
            var bodySpan = bodySyntax.Span;

            foreach (var reference in local.DeclaringSyntaxReferences)
            {
                if (reference.SyntaxTree == bodyTree && bodySpan.Contains(reference.Span))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Report(
            OperationAnalysisContext context,
            HashSet<SyntaxNode> reported,
            SyntaxNode syntax,
            string message)
        {
            if (!reported.Add(syntax))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.GetLocation(), message));
        }

        private static bool IsInsideNestedFunction(IOperation op, IOperation root)
        {
            for (var current = op.Parent; current is not null && current != root; current = current.Parent)
            {
                if (current is IAnonymousFunctionOperation || current is ILocalFunctionOperation)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInsideInnerLoop(IOperation op, ILoopOperation outerLoop)
        {
            // Walk up from `op` looking for a loop operation that is strictly inside `outerLoop`.
            for (var current = op.Parent; current is not null && current != outerLoop; current = current.Parent)
            {
                if (current is ILoopOperation && current != outerLoop)
                {
                    return true;
                }
            }

            return false;
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
    }
}

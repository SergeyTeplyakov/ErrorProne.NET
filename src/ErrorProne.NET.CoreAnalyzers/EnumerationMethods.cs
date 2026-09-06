// --------------------------------------------------------------------
//  
// Copyright (c) Microsoft Corporation.  All rights reserved.
//  
// --------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// Shared helpers for analyzers that reason about how an <see cref="IEnumerable{T}"/>
    /// is consumed (EPC38, EPC39).
    /// </summary>
    internal static class EnumerationMethods
    {
        /// <summary>
        /// Names of <see cref="System.Linq.Enumerable"/> methods that fully materialize / iterate the source
        /// (i.e. they call <c>GetEnumerator()</c> at least once when invoked).
        /// </summary>
        private static readonly ImmutableHashSet<string> EnumeratingLinqMethods = ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "Aggregate",
            "All",
            "Any",
            "Average",
            "Contains",
            "Count",
            "ElementAt",
            "ElementAtOrDefault",
            "First",
            "FirstOrDefault",
            "Last",
            "LastOrDefault",
            "LongCount",
            "Max",
            "MaxBy",
            "Min",
            "MinBy",
            "SequenceEqual",
            "Single",
            "SingleOrDefault",
            "Sum",
            "ToArray",
            "ToDictionary",
            "ToFrozenDictionary",
            "ToFrozenSet",
            "ToHashSet",
            "ToList",
            "ToLookup");

        /// <summary>
        /// Names of <see cref="System.Linq.Enumerable"/> methods that produce a *new*, materialized collection
        /// from the source.
        /// </summary>
        private static readonly ImmutableHashSet<string> MaterializingLinqMethods = ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "ToArray",
            "ToDictionary",
            "ToFrozenDictionary",
            "ToFrozenSet",
            "ToHashSet",
            "ToList",
            "ToLookup");

        /// <summary>
        /// Names of <see cref="System.Linq.Enumerable"/> methods that return a deferred enumerable
        /// (LINQ chain methods). They do not enumerate their source.
        /// </summary>
        private static readonly ImmutableHashSet<string> ChainLinqMethods = ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "Append",
            "AsEnumerable",
            "Cast",
            "Concat",
            "DefaultIfEmpty",
            "Distinct",
            "DistinctBy",
            "Except",
            "ExceptBy",
            "GroupBy",
            "GroupJoin",
            "Intersect",
            "IntersectBy",
            "Join",
            "OfType",
            "OrderBy",
            "OrderByDescending",
            "Prepend",
            "Reverse",
            "Select",
            "SelectMany",
            "Skip",
            "SkipLast",
            "SkipWhile",
            "Take",
            "TakeLast",
            "TakeWhile",
            "ThenBy",
            "ThenByDescending",
            "Union",
            "UnionBy",
            "Where",
            "Zip");

        /// <summary>
        /// Names of methods on <see cref="System.Threading.Tasks.Task"/> that take an
        /// <see cref="IEnumerable{T}"/> of tasks and observe every element.
        /// </summary>
        private static readonly ImmutableHashSet<string> TaskAggregationMethods = ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "WhenAll",
            "WhenAny",
            "WaitAll",
            "WaitAny");

        /// <summary>
        /// Names of static <see cref="System.Linq.Enumerable"/> methods that *produce* a new deferred
        /// <see cref="IEnumerable{T}"/> from non-enumerable inputs (so every consumer pass starts a fresh
        /// iteration).
        /// </summary>
        private static readonly ImmutableHashSet<string> EnumerableProducerMethods = ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "Empty",
            "Range",
            "Repeat");

        /// <summary>
        /// Returns <see langword="true"/> if invoking <paramref name="method"/> consumes its source enumerable
        /// at least once (e.g. <c>Count()</c>, <c>ToList()</c>, <c>Any()</c>, …).
        /// </summary>
        public static bool IsEnumeratingLinqMethod(IMethodSymbol method, Compilation compilation)
        {
            if (method.MethodKind != MethodKind.Ordinary && method.MethodKind != MethodKind.ReducedExtension)
            {
                return false;
            }

            if (!EnumeratingLinqMethods.Contains(method.Name))
            {
                return false;
            }

            var containing = (method.ReducedFrom ?? method).ContainingType;
            return containing.IsClrType(compilation, typeof(System.Linq.Enumerable));
        }

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="method"/> is a LINQ chain method
        /// (<c>Where</c>, <c>Select</c>, <c>OrderBy</c>, …) that produces a new deferred enumerable.
        /// </summary>
        public static bool IsLinqChainMethod(IMethodSymbol method, Compilation compilation)
        {
            if (!ChainLinqMethods.Contains(method.Name))
            {
                return false;
            }

            var containing = (method.ReducedFrom ?? method).ContainingType;
            return containing.IsClrType(compilation, typeof(System.Linq.Enumerable));
        }

        /// <summary>
        /// Returns <see langword="true"/> if invoking <paramref name="method"/> produces a freshly
        /// materialized collection from the source (the source is consumed but the *result* is no longer deferred).
        /// </summary>
        public static bool IsMaterializingLinqMethod(IMethodSymbol method, Compilation compilation)
        {
            if (!MaterializingLinqMethods.Contains(method.Name))
            {
                return false;
            }

            var containing = (method.ReducedFrom ?? method).ContainingType;
            return containing.IsClrType(compilation, typeof(System.Linq.Enumerable));
        }

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="method"/> is <c>Task.WhenAll</c>,
        /// <c>Task.WhenAny</c>, <c>Task.WaitAll</c>, or <c>Task.WaitAny</c>.
        /// </summary>
        public static bool IsTaskAggregationMethod(IMethodSymbol method, Compilation compilation)
        {
            if (!method.IsStatic || !TaskAggregationMethods.Contains(method.Name))
            {
                return false;
            }

            return method.ContainingType.IsClrType(compilation, typeof(System.Threading.Tasks.Task));
        }

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="method"/> is one of
        /// <c>Enumerable.Range</c>, <c>Enumerable.Repeat</c>, or <c>Enumerable.Empty</c> — i.e.
        /// a static factory that produces a fresh deferred enumerable each time it is invoked.
        /// </summary>
        public static bool IsEnumerableProducerMethod(IMethodSymbol method, Compilation compilation)
        {
            if (!method.IsStatic || !EnumerableProducerMethods.Contains(method.Name))
            {
                return false;
            }

            return method.ContainingType.IsClrType(compilation, typeof(System.Linq.Enumerable));
        }

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="type"/> is the deferred <see cref="IEnumerable{T}"/>
        /// or non-generic <see cref="System.Collections.IEnumerable"/> interface itself. Concrete materialized
        /// collections (<c>T[]</c>, <c>List&lt;T&gt;</c>, <c>IReadOnlyList&lt;T&gt;</c>, <c>HashSet&lt;T&gt;</c>, …)
        /// return <see langword="false"/>.
        /// </summary>
        public static bool IsDeferredEnumerableType(ITypeSymbol? type)
        {
            if (type is null)
            {
                return false;
            }

            // SpecialType lives on the original (unconstructed) definition. For a constructed type like
            // IEnumerable<Task<int>>, type.SpecialType is None — we must inspect OriginalDefinition.
            switch (type.OriginalDefinition.SpecialType)
            {
                case SpecialType.System_Collections_Generic_IEnumerable_T:
                case SpecialType.System_Collections_IEnumerable:
                    return true;
            }

            // ParallelQuery<T>, OrderedEnumerable<T>, IGrouping<TKey, T>, IOrderedEnumerable<T>, etc. are
            // intentionally NOT included to keep the rule precise. The most common deferred shape from a LINQ
            // chain has the static type IEnumerable<T>, which is handled above.
            return false;
        }

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="type"/> is <see cref="IEnumerable{T}"/>
        /// of <see cref="System.Threading.Tasks.Task"/>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c>, or
        /// <c>ValueTask&lt;T&gt;</c>.
        /// </summary>
        public static bool IsTaskEnumerableType(ITypeSymbol? type, Compilation compilation)
        {
            if (type is not INamedTypeSymbol named)
            {
                return false;
            }

            if (!IsDeferredEnumerableType(named))
            {
                return false;
            }

            if (named.TypeArguments.Length != 1)
            {
                return false;
            }

            return named.TypeArguments[0].IsTaskLike(compilation);
        }

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="type"/> is some materialized
        /// <c>T[]</c>/<c>List&lt;T&gt;</c>/<c>IReadOnlyList&lt;T&gt;</c>/<c>IList&lt;T&gt;</c>/
        /// <c>ICollection&lt;T&gt;</c>/<c>IReadOnlyCollection&lt;T&gt;</c>/<c>ImmutableArray&lt;T&gt;</c>/
        /// <c>HashSet&lt;T&gt;</c> — i.e. cases where re-enumeration is safe.
        /// </summary>
        public static bool IsMaterializedCollectionType(ITypeSymbol? type)
        {
            if (type is null)
            {
                return false;
            }

            if (type.TypeKind == TypeKind.Array)
            {
                return true;
            }

            // Match by metadata name to avoid one-by-one compilation lookups (and to handle missing references).
            return type.OriginalDefinition.SpecialType switch
            {
                SpecialType.System_Collections_Generic_ICollection_T => true,
                SpecialType.System_Collections_Generic_IList_T => true,
                SpecialType.System_Collections_Generic_IReadOnlyCollection_T => true,
                SpecialType.System_Collections_Generic_IReadOnlyList_T => true,
                _ => IsMaterializedByMetadataName(type),
            };
        }

        private static bool IsMaterializedByMetadataName(ITypeSymbol type)
        {
            var name = type.OriginalDefinition.ToDisplayString();
            return name switch
            {
                "System.Collections.Generic.List<T>" => true,
                "System.Collections.Generic.HashSet<T>" => true,
                "System.Collections.Generic.Dictionary<TKey, TValue>" => true,
                "System.Collections.Generic.SortedSet<T>" => true,
                "System.Collections.Generic.SortedList<TKey, TValue>" => true,
                "System.Collections.Generic.SortedDictionary<TKey, TValue>" => true,
                "System.Collections.Generic.Queue<T>" => true,
                "System.Collections.Generic.Stack<T>" => true,
                "System.Collections.Immutable.ImmutableArray<T>" => true,
                "System.Collections.Immutable.ImmutableList<T>" => true,
                "System.Collections.Immutable.ImmutableHashSet<T>" => true,
                "System.Collections.Immutable.ImmutableSortedSet<T>" => true,
                "System.Collections.Immutable.ImmutableDictionary<TKey, TValue>" => true,
                "System.Collections.Frozen.FrozenSet<T>" => true,
                "System.Collections.Frozen.FrozenDictionary<TKey, TValue>" => true,
                "System.Collections.Concurrent.ConcurrentBag<T>" => true,
                _ => false,
            };
        }

        /// <summary>
        /// Walks back through LINQ chain calls (<c>x.Where(...).Select(...).OrderBy(...)</c>) and casts/conversions
        /// to find the underlying local/parameter that the chain ultimately reads.
        ///
        /// Returns the root <see cref="ISymbol"/> if it is a <see cref="ILocalSymbol"/> or
        /// <see cref="IParameterSymbol"/>, otherwise <see langword="null"/>.
        /// </summary>
        public static ISymbol? TryGetRootEnumerableSymbol(IOperation? operation, Compilation compilation)
        {
            while (operation is not null)
            {
                switch (operation)
                {
                    case IConversionOperation conv:
                        operation = conv.Operand;
                        continue;

                    case IParenthesizedOperation paren:
                        operation = paren.Operand;
                        continue;

                    case ILocalReferenceOperation local:
                        return local.Local;

                    case IParameterReferenceOperation parameter:
                        return parameter.Parameter;

                    case IInvocationOperation invocation:
                    {
                        // LINQ chain: keep walking back through `.Where(...).Select(...)` to the chain root.
                        if (IsLinqChainMethod(invocation.TargetMethod, compilation))
                        {
                            // For an extension method, the source is the first argument; for the (rare)
                            // explicit Enumerable.Xxx(source, ...) form it is the same.
                            if (invocation.Arguments.Length > 0)
                            {
                                operation = invocation.Arguments[0].Value;
                                continue;
                            }

                            operation = invocation.Instance;
                            continue;
                        }

                        // Any other invocation is a wall; we cannot reason about it.
                        return null;
                    }

                    default:
                        return null;
                }
            }

            return null;
        }
    }
}

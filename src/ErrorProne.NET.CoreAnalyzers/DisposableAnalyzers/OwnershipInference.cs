using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.DisposableAnalyzers;

internal sealed class OwnershipInference
{
    private readonly Compilation _compilation;
    private readonly OwnershipContracts _contracts;
    private readonly DisposeAnalysisHelper _helper;
    private readonly IMethodSymbol? _configureAsyncDisposable;
    private readonly HashSet<IMethodSymbol> _configureAwaitMethods;
    private readonly ConcurrentDictionary<IParameterSymbol, bool> _acquires = new(SymbolEqualityComparer.Default);
    private readonly ConcurrentDictionary<IMethodSymbol, bool> _freshReturns = new(SymbolEqualityComparer.Default);

    public OwnershipInference(Compilation compilation, OwnershipContracts contracts, DisposeAnalysisHelper helper)
    {
        _compilation = compilation;
        _contracts = contracts;
        _helper = helper;
        _configureAsyncDisposable = compilation.GetTypeByMetadataName("System.Threading.Tasks.TaskAsyncEnumerableExtensions")
            ?.GetMembers("ConfigureAwait").OfType<IMethodSymbol>().FirstOrDefault(method =>
                SymbolEqualityComparer.Default.Equals(method.ReturnType, helper.IConfigureAsyncDisposable));
        _configureAwaitMethods = new HashSet<IMethodSymbol>(new[]
            {
                "System.Threading.Tasks.Task", "System.Threading.Tasks.Task`1",
                "System.Threading.Tasks.ValueTask", "System.Threading.Tasks.ValueTask`1",
            }.SelectMany(name => compilation.GetTypeByMetadataName(name)?.GetMembers("ConfigureAwait")
                .OfType<IMethodSymbol>() ?? Enumerable.Empty<IMethodSymbol>()), SymbolEqualityComparer.Default);
    }

    public IOperation? GetConfiguredDisposableResource(IInvocationOperation invocation)
    {
        var method = (invocation.TargetMethod.ReducedFrom ?? invocation.TargetMethod).OriginalDefinition;
        if (_configureAsyncDisposable == null
            || !SymbolEqualityComparer.Default.Equals(method, _configureAsyncDisposable)
            || _contracts.GetReturnOwnership(invocation.TargetMethod) != OwnershipKind.Unspecified)
        {
            return null;
        }

        return invocation.Instance ?? invocation.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == 0)?.Value;
    }

    public ISymbol? GetAwaitedMember(IOperation operation)
    {
        operation = DisposeBeforeLosingScopeAnalyzer.Unwrap(operation);
        if (operation is IInvocationOperation { Instance: { } instance } invocation
            && _configureAwaitMethods.Contains(invocation.TargetMethod.OriginalDefinition)
            && _contracts.GetReturnOwnership(invocation.TargetMethod) == OwnershipKind.Unspecified)
        {
            return GetAwaitedMember(instance);
        }

        return operation switch
        {
            IInvocationOperation call => call.TargetMethod,
            IPropertyReferenceOperation property => property.Property,
            IConversionOperation conversion => conversion.OperatorMethod,
            _ => null,
        };
    }

    public IOperation? GetInterlockedOwningValue(IInvocationOperation invocation)
    {
        if (invocation.TargetMethod.Name != "Exchange" || invocation.Arguments.Length != 2
            || !SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType,
                _compilation.GetTypeByMetadataName("System.Threading.Interlocked")))
        {
            return null;
        }

        var target = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == 0);
        return target != null && DisposeBeforeLosingScopeAnalyzer.Unwrap(target.Value) is IMemberReferenceOperation member
            && !_contracts.IsBorrowed(member.Member)
            ? invocation.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == 1)?.Value : null;
    }

    public bool AcquiresConversionInput(IConversionOperation conversion, CancellationToken cancellationToken)
    {
        return conversion.OperatorMethod is { Parameters.Length: 1 } method
            && AcquiresOwnership(method.Parameters[0], ImmutableArray<IArgumentOperation>.Empty, cancellationToken);
    }

    public bool ReturnsOwnership(IMethodSymbol method, CancellationToken cancellationToken)
    {
        var contract = _contracts.GetReturnOwnership(method);
        if (contract != OwnershipKind.Unspecified)
        {
            return contract == OwnershipKind.Owned;
        }

        return HasFreshReturn(method, cancellationToken);
    }

    public bool IsFluentAlias(IMethodSymbol method, CancellationToken cancellationToken)
    {
        if (_contracts.GetReturnOwnership(method) != OwnershipKind.Unspecified)
        {
            return false;
        }

        var candidate = (!method.IsStatic && SymbolEqualityComparer.Default.Equals(method.ReturnType, method.ContainingType))
            || (method.IsExtensionMethod && method.OriginalDefinition.ReturnType is ITypeParameterSymbol
                && method.Parameters.Length > 0
                && SymbolEqualityComparer.Default.Equals(method.ReturnType, method.Parameters[0].Type));
        return candidate && !HasFreshReturn(method, cancellationToken);
    }

    private bool HasFreshReturn(IMethodSymbol method, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var definition = (method.ReducedFrom ?? method).OriginalDefinition;
        return _freshReturns.GetOrAdd(definition, m => InferFreshReturn(m, cancellationToken));
    }

    private bool InferFreshReturn(IMethodSymbol method, CancellationToken cancellationToken)
    {
        method = method.PartialImplementationPart ?? method;
        if (method.MethodKind != MethodKind.Ordinary || method.IsAbstract || method.IsExtern
            || ((method.IsVirtual || method.IsOverride) && !method.IsSealed && !method.ContainingType.IsSealed))
        {
            return false;
        }

        foreach (var operation in GetSourceOperations(method, cancellationToken))
        {
            if (operation is not IMethodBodyOperation body)
            {
                continue;
            }

            // Only a direct return: no branch joins, local alias tracking, or factory forwarding.
            var block = body.BlockBody ?? body.ExpressionBody;
            if (block?.Operations.Length == 1
                && block.Operations[0] is IReturnOperation { Kind: OperationKind.Return, ReturnedValue: { } value })
            {
                return IsFreshCreation(value);
            }
        }

        return false;
    }

    private IEnumerable<IOperation> GetSourceOperations(ISymbol symbol, CancellationToken cancellationToken)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Project references can expose syntax from a different compilation.
            if (!_compilation.ContainsSyntaxTree(reference.SyntaxTree))
            {
                continue;
            }

            var syntax = reference.GetSyntax(cancellationToken);
            var model = _compilation.GetSemanticModel(syntax.SyntaxTree);
            if (model.GetOperation(syntax, cancellationToken) is { } operation)
            {
                yield return operation;
            }
        }
    }

    private bool IsFreshCreation(IOperation value)
    {
        while (true)
        {
            switch (value)
            {
                case IParenthesizedOperation parenthesized:
                    value = parenthesized.Operand;
                    break;
                case IConversionOperation conversion when conversion.Conversion.Exists
                    && !conversion.Conversion.IsUserDefined
                    && conversion.Type?.TypeKind != TypeKind.Dynamic
                    && conversion.Operand.Type?.TypeKind != TypeKind.Dynamic:
                    value = conversion.Operand;
                    break;
                default:
                    return value is IObjectCreationOperation or ITypeParameterObjectCreationOperation
                        && _helper.ShouldBeDisposed(value.Type);
            }
        }
    }

    public bool AcquiresOwnership(IParameterSymbol parameter, ImmutableArray<IArgumentOperation> arguments,
        CancellationToken cancellationToken)
    {
        return KnownAcquisition(parameter, arguments) ?? _acquires.GetOrAdd(parameter.OriginalDefinition,
            p => InferAcquisition(p, new HashSet<IParameterSymbol>(SymbolEqualityComparer.Default), cancellationToken));
    }

    private bool? KnownAcquisition(IParameterSymbol parameter, ImmutableArray<IArgumentOperation> arguments)
    {
        if (_contracts.AcquiresOwnership(parameter))
        {
            return true;
        }

        if (_contracts.IsBorrowed(parameter))
        {
            return false;
        }

        if (IsStreamWrapperParameter(parameter))
        {
            var leaveOpen = arguments.FirstOrDefault(a => a.Parameter?.Name == "leaveOpen");
            return leaveOpen == null || leaveOpen.Value.ConstantValue is { HasValue: true, Value: false };
        }

        return null;
    }

    private bool InferAcquisition(IParameterSymbol parameter, HashSet<IParameterSymbol> visiting,
        CancellationToken cancellationToken)
    {
        if (_contracts.AcquiresOwnership(parameter))
        {
            return true;
        }

        if (_contracts.IsBorrowed(parameter) || visiting.Count >= 4 || !visiting.Add(parameter))
        {
            return false;
        }

        try
        {
            foreach (var root in GetSourceOperations(parameter.ContainingSymbol, cancellationToken))
            {
                var aliases = new HashSet<ISymbol>(SymbolEqualityComparer.Default) { parameter };
                foreach (var operation in DisposeBeforeLosingScopeAnalyzer.EnumerateOperations(root))
                {
                    if (operation is IInvocationOperation invocation)
                    {
                        if ((IsDisposeCall(invocation) && ReferencesResource(invocation.Instance, aliases))
                            || ReferencesResource(GetInterlockedOwningValue(invocation), aliases))
                        {
                            return true;
                        }

                        if (ForwardsOwnership(invocation.Arguments, aliases, visiting, cancellationToken))
                        {
                            return true;
                        }
                    }
                    else if (operation is IObjectCreationOperation creation
                             && ForwardsOwnership(creation.Arguments, aliases, visiting, cancellationToken))
                    {
                        return true;
                    }
                    else if (operation is IPropertyReferenceOperation property
                             && ForwardsOwnership(property.Arguments, aliases, visiting, cancellationToken))
                    {
                        return true;
                    }
                    else if (operation is IConversionOperation { OperatorMethod: { Parameters.Length: 1 } method } conversion
                             && ReferencesResource(conversion.Operand, aliases)
                             && (KnownAcquisition(method.Parameters[0], ImmutableArray<IArgumentOperation>.Empty)
                                 ?? InferAcquisition(method.Parameters[0].OriginalDefinition, visiting, cancellationToken)))
                    {
                        return true;
                    }
                    else if (operation is IUsingDeclarationOperation declaration && declaration.DeclarationGroup.Declarations
                             .SelectMany(d => d.Declarators).Any(d => aliases.Contains(d.Symbol)))
                    {
                        return true;
                    }
                    else if (operation is IVariableDeclaratorOperation { Initializer: { } initializer } declarator
                             && ReferencesResource(initializer.Value, aliases))
                    {
                        aliases.Add(declarator.Symbol);
                    }
                    else if (operation is ISimpleAssignmentOperation assignment)
                    {
                        var target = DisposeBeforeLosingScopeAnalyzer.GetAliasSymbol(assignment.Target);
                        if (ReferencesResource(assignment.Value, aliases))
                        {
                            if (assignment.Target is IMemberReferenceOperation member && !_contracts.IsBorrowed(member.Member))
                            {
                                return true;
                            }

                            if (target != null)
                            {
                                aliases.Add(target);
                            }
                        }
                        else if (target != null && !DisposeBeforeLosingScopeAnalyzer.IsConditional(assignment))
                        {
                            aliases.Remove(target);
                        }
                    }

                    if (DisposeBeforeLosingScopeAnalyzer.GetUsingCapture(operation) is { } captured
                        && (ReferencesResource(operation, aliases) || captured.Locals.Any(aliases.Contains)))
                    {
                        return true;
                    }
                }
            }
        }
        finally
        {
            visiting.Remove(parameter);
        }

        return false;
    }

    private bool ForwardsOwnership(ImmutableArray<IArgumentOperation> arguments, HashSet<ISymbol> aliases,
        HashSet<IParameterSymbol> visiting, CancellationToken cancellationToken)
    {
        return arguments.Any(a => a.Parameter != null && ReferencesResource(a.Value, aliases)
            && (KnownAcquisition(a.Parameter, arguments)
                ?? InferAcquisition(a.Parameter.OriginalDefinition, visiting, cancellationToken)));
    }

    private static bool IsStreamWrapperParameter(IParameterSymbol parameter)
    {
        return parameter.ContainingSymbol is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
            && parameter.Type.ToDisplayString() == "System.IO.Stream"
            && constructor.ContainingType.ToDisplayString() is
                "System.IO.StreamReader" or "System.IO.StreamWriter" or "System.IO.BinaryReader" or "System.IO.BinaryWriter";
    }

    internal static bool IsDisposeCall(IInvocationOperation invocation)
    {
        return !invocation.TargetMethod.IsStatic && invocation.Arguments.IsEmpty
            && ((invocation.TargetMethod.Name is "Dispose" or "Close" && invocation.TargetMethod.ReturnsVoid)
                || invocation.TargetMethod.Name == "DisposeAsync");
    }

    private bool ReferencesResource(IOperation? operation, HashSet<ISymbol> aliases)
    {
        if (operation == null)
        {
            return false;
        }

        operation = DisposeBeforeLosingScopeAnalyzer.Unwrap(operation);
        if (DisposeBeforeLosingScopeAnalyzer.GetAliasSymbol(operation) is { } symbol)
        {
            return aliases.Contains(symbol);
        }

        if (operation is IConditionalAccessInstanceOperation)
        {
            return ReferencesResource(DisposeBeforeLosingScopeAnalyzer.GetConditionalReceiver(operation), aliases);
        }

        if (operation is IInvocationOperation invocation && GetConfiguredDisposableResource(invocation) is { } resource)
        {
            return ReferencesResource(resource, aliases);
        }

        if (operation is ISimpleAssignmentOperation assignment)
        {
            return ReferencesResource(assignment.Value, aliases);
        }

        return false;
    }
}

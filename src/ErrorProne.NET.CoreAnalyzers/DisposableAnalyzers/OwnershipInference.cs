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
    private readonly IMethodSymbol? _disposeAsync;
    private readonly IMethodSymbol? _configuredDisposeAsync;
    private readonly HashSet<IMethodSymbol> _configureAwaitMethods;
    private readonly HashSet<IMethodSymbol> _completedTaskFactories;
    private readonly INamedTypeSymbol? _valueTaskOfT;
    private readonly ConcurrentDictionary<IParameterSymbol, bool> _acquires = new(SymbolEqualityComparer.Default);
    private readonly ConcurrentDictionary<IMethodSymbol, bool> _freshReturns = new(SymbolEqualityComparer.Default);

    public OwnershipInference(Compilation compilation, OwnershipContracts contracts, DisposeAnalysisHelper helper)
    {
        _compilation = compilation;
        _contracts = contracts;
        _helper = helper;
        _disposeAsync = helper.IAsyncDisposable?.GetMembers("DisposeAsync").OfType<IMethodSymbol>()
            .FirstOrDefault(method => !method.IsStatic && method.Parameters.IsEmpty);
        _configuredDisposeAsync = helper.IConfigureAsyncDisposable?.GetMembers("DisposeAsync").OfType<IMethodSymbol>()
            .FirstOrDefault(method => !method.IsStatic && method.Parameters.IsEmpty);
        _configureAsyncDisposable = compilation.GetTypeByMetadataName("System.Threading.Tasks.TaskAsyncEnumerableExtensions")
            ?.GetMembers("ConfigureAwait").OfType<IMethodSymbol>().FirstOrDefault(method =>
                SymbolEqualityComparer.Default.Equals(method.ReturnType, helper.IConfigureAsyncDisposable));
        _configureAwaitMethods = new HashSet<IMethodSymbol>(new[]
            {
                "System.Threading.Tasks.Task", "System.Threading.Tasks.Task`1",
                "System.Threading.Tasks.ValueTask", "System.Threading.Tasks.ValueTask`1",
            }.SelectMany(name => compilation.GetTypeByMetadataName(name)?.GetMembers("ConfigureAwait")
                .OfType<IMethodSymbol>() ?? Enumerable.Empty<IMethodSymbol>()), SymbolEqualityComparer.Default);
        _completedTaskFactories = new HashSet<IMethodSymbol>(new[]
            {
                "System.Threading.Tasks.Task", "System.Threading.Tasks.ValueTask",
            }.SelectMany(name => compilation.GetTypeByMetadataName(name)?.GetMembers("FromResult")
                .OfType<IMethodSymbol>() ?? Enumerable.Empty<IMethodSymbol>()), SymbolEqualityComparer.Default);
        _valueTaskOfT = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");
    }

    public IOperation? GetCompletedTaskValue(IOperation operation)
    {
        operation = DisposeBeforeLosingScopeAnalyzer.Unwrap(operation);
        return operation switch
        {
            IInvocationOperation invocation when _completedTaskFactories.Contains(invocation.TargetMethod.OriginalDefinition)
                && _contracts.GetReturnOwnership(invocation.TargetMethod) == OwnershipKind.Unspecified
                => invocation.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == 0)?.Value,
            IObjectCreationOperation { Constructor: { Parameters.Length: 1 } constructor } creation
                when SymbolEqualityComparer.Default.Equals(constructor.ContainingType.OriginalDefinition, _valueTaskOfT)
                    && constructor.OriginalDefinition.Parameters[0].Type is ITypeParameterSymbol
                => creation.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == 0)?.Value,
            _ => null,
        };
    }

    internal bool IsTaskResultCarrier(ITypeSymbol? type) =>
        type.IsTaskLike(_compilation, TaskLikeTypes.TaskOfT | TaskLikeTypes.ValueTaskOfT);

    public IOperation? GetConfiguredTask(IOperation operation)
    {
        return operation is IInvocationOperation { Instance: { } instance } invocation
            && _configureAwaitMethods.Contains(invocation.TargetMethod.OriginalDefinition)
            && _contracts.GetReturnOwnership(invocation.TargetMethod) == OwnershipKind.Unspecified
            ? instance : null;
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
        if (GetConfiguredTask(operation) is { } instance)
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

    internal static IEnumerable<(IParameterSymbol Parameter, IOperation Value)> GetOperatorInputs(IOperation operation)
    {
        switch (operation)
        {
            case IConversionOperation { OperatorMethod: { Parameters.Length: 1 } method } conversion:
                yield return (method.Parameters[0], conversion.Operand);
                break;
            case IUnaryOperation { OperatorMethod: { Parameters.Length: 1 } method } unary:
                yield return (method.Parameters[0], unary.Operand);
                break;
            case IBinaryOperation { OperatorMethod: { Parameters.Length: 2 } method } binary:
                yield return (method.Parameters[0], binary.LeftOperand);
                yield return (method.Parameters[1], binary.RightOperand);
                break;
            case IIncrementOrDecrementOperation { OperatorMethod: { Parameters.Length: 1 } method } increment:
                yield return (method.Parameters[0], increment.Target);
                break;
            case ICompoundAssignmentOperation { OperatorMethod: { Parameters.Length: 2 } method } assignment:
                yield return (method.Parameters[0], assignment.Target);
                yield return (method.Parameters[1], assignment.Value);
                break;
        }
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

    public IOperation? GetFluentReceiver(IInvocationOperation invocation, CancellationToken cancellationToken = default)
    {
        return IsFluentAlias(invocation.TargetMethod, cancellationToken)
            ? invocation.Instance ?? invocation.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == 0)?.Value
            : null;
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

    public bool IsUnknownArgument(IArgumentOperation argument, ImmutableArray<IArgumentOperation> arguments)
    {
        return argument.Parameter is not { } parameter
            || KnownAcquisition(parameter, arguments) == null;
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
            return leaveOpen == null ? true
                : leaveOpen.Value.ConstantValue is { HasValue: true, Value: bool value } ? !value
                : null;
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
                var aliases = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
                var carriers = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
                (IsTaskResultCarrier(parameter.Type) ? carriers : aliases).Add(parameter);
                foreach (var operation in DisposeBeforeLosingScopeAnalyzer.EnumerateOperations(root))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (DisposeBeforeLosingScopeAnalyzer.GetPatternAlias(operation) is { } patternAlias)
                    {
                        TrackAlias(patternAlias.Local, patternAlias.Value, conditional: true);
                    }

                    if (GetOperatorInputs(operation).Any(input =>
                        (ReferencesResource(input.Value, aliases, carriers) || ReferencesCarrier(input.Value, aliases, carriers))
                        && (KnownAcquisition(input.Parameter, ImmutableArray<IArgumentOperation>.Empty)
                            ?? InferAcquisition(input.Parameter.OriginalDefinition, visiting, cancellationToken))))
                    {
                        return true;
                    }

                    if (operation is IInvocationOperation invocation)
                    {
                        var published = GetInterlockedOwningValue(invocation);
                        if ((IsDisposeCall(invocation) && ReferencesResource(invocation.Instance, aliases, carriers))
                            || ReferencesResource(published, aliases, carriers) || ReferencesCarrier(published, aliases, carriers))
                        {
                            return true;
                        }

                        if (ForwardsOwnership(invocation.Arguments, aliases, carriers, visiting, cancellationToken))
                        {
                            return true;
                        }
                    }
                    else if (operation is IObjectCreationOperation creation
                             && ForwardsOwnership(creation.Arguments, aliases, carriers, visiting, cancellationToken))
                    {
                        return true;
                    }
                    else if (operation is IPropertyReferenceOperation property
                             && ForwardsOwnership(property.Arguments, aliases, carriers, visiting, cancellationToken))
                    {
                        return true;
                    }
                    else if (operation is IUsingDeclarationOperation declaration && declaration.DeclarationGroup.Declarations
                             .SelectMany(d => d.Declarators).Any(d => aliases.Contains(d.Symbol)))
                    {
                        return true;
                    }
                    else if (operation is IVariableDeclaratorOperation { Initializer: { } initializer } declarator)
                    {
                        TrackAlias(declarator.Symbol, initializer.Value, conditional: false);
                    }
                    else if (operation is ISimpleAssignmentOperation assignment)
                    {
                        var target = DisposeBeforeLosingScopeAnalyzer.GetAliasSymbol(assignment.Target);
                        if ((ReferencesResource(assignment.Value, aliases, carriers)
                                || ReferencesCarrier(assignment.Value, aliases, carriers))
                            && assignment.Target is IMemberReferenceOperation member && !_contracts.IsBorrowed(member.Member))
                        {
                            return true;
                        }

                        if (target != null)
                        {
                            TrackAlias(target, assignment.Value, DisposeBeforeLosingScopeAnalyzer.IsConditional(assignment));
                        }
                    }

                    if (DisposeBeforeLosingScopeAnalyzer.GetUsingCapture(operation) is { } captured
                        && (ReferencesResource(operation, aliases, carriers) || captured.Locals.Any(aliases.Contains)))
                    {
                        return true;
                    }
                }

                void TrackAlias(ISymbol target, IOperation value, bool conditional)
                {
                    var resource = ReferencesResource(value, aliases, carriers);
                    var carrier = ReferencesCarrier(value, aliases, carriers);
                    if (resource)
                    {
                        aliases.Add(target);
                    }
                    else if (!conditional)
                    {
                        aliases.Remove(target);
                    }

                    if (carrier)
                    {
                        carriers.Add(target);
                    }
                    else if (!conditional)
                    {
                        carriers.Remove(target);
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

    private bool ForwardsOwnership(ImmutableArray<IArgumentOperation> arguments, HashSet<ISymbol> aliases, HashSet<ISymbol> carriers,
        HashSet<IParameterSymbol> visiting, CancellationToken cancellationToken)
    {
        return arguments.Any(a => a.Parameter != null
            && (ReferencesResource(a.Value, aliases, carriers) || ReferencesCarrier(a.Value, aliases, carriers))
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

    internal bool IsDisposeCall(IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;
        if (method.IsStatic || !invocation.Arguments.IsEmpty)
        {
            return false;
        }

        if (method.Name is "Dispose" or "Close" && method.ReturnsVoid)
        {
            return true;
        }

        if (method.Name != "DisposeAsync")
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(method.OriginalDefinition, _disposeAsync)
            || SymbolEqualityComparer.Default.Equals(method.OriginalDefinition, _configuredDisposeAsync))
        {
            return true;
        }

        var receiver = invocation.Instance?.Type as INamedTypeSymbol ?? method.ContainingType;
        var implementation = _disposeAsync == null ? null : receiver.FindImplementationForInterfaceMember(_disposeAsync);
        for (IMethodSymbol? candidate = method; candidate != null; candidate = candidate.OverriddenMethod)
        {
            if (SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, implementation?.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    private bool ReferencesResource(IOperation? operation, HashSet<ISymbol> aliases, HashSet<ISymbol> carriers)
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
            return ReferencesResource(DisposeBeforeLosingScopeAnalyzer.GetConditionalReceiver(operation), aliases, carriers);
        }

        if (operation is IInvocationOperation invocation && GetConfiguredDisposableResource(invocation) is { } resource)
        {
            return ReferencesResource(resource, aliases, carriers);
        }

        if (operation is ISimpleAssignmentOperation assignment)
        {
            return ReferencesResource(assignment.Value, aliases, carriers);
        }

        return operation is IAwaitOperation awaited && ReferencesCarrier(awaited.Operation, aliases, carriers);
    }

    private bool ReferencesCarrier(IOperation? operation, HashSet<ISymbol> aliases, HashSet<ISymbol> carriers)
    {
        if (operation == null)
        {
            return false;
        }

        operation = DisposeBeforeLosingScopeAnalyzer.Unwrap(operation);
        if (DisposeBeforeLosingScopeAnalyzer.GetAliasSymbol(operation) is { } symbol)
        {
            return carriers.Contains(symbol);
        }

        if (GetCompletedTaskValue(operation) is { } result)
        {
            return ReferencesResource(result, aliases, carriers);
        }

        if (GetConfiguredTask(operation) is { } task)
        {
            return ReferencesCarrier(task, aliases, carriers);
        }

        if (operation is IInvocationOperation invocation && GetFluentReceiver(invocation) is { } receiver)
        {
            return ReferencesCarrier(receiver, aliases, carriers);
        }

        return operation is ISimpleAssignmentOperation assignment
            && ReferencesCarrier(assignment.Value, aliases, carriers);
    }
}

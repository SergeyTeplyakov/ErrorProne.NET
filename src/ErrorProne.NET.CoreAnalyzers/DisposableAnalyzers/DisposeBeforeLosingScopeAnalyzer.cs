using System;
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
using Microsoft.CodeAnalysis.Text;

namespace ErrorProne.NET.DisposableAnalyzers;

/// <summary>
/// Checks local ownership obligations using explicit contracts and bounded source inference.
/// Conditional cleanup is accepted; this is not an exception-safe or path-sensitive proof.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DisposeBeforeLosingScopeAnalyzer : DiagnosticAnalyzerBase
{
    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptors.ERP044;

    public override bool ReportDiagnosticsOnGeneratedCode => false;

    public DisposeBeforeLosingScopeAnalyzer()
        : base(Rule, DiagnosticDescriptors.ERP045, DiagnosticDescriptors.ERP046)
    {
    }

    protected override void InitializeCore(AnalysisContext context)
    {
        context.RegisterCompilationStartAction(start =>
        {
            var contracts = OwnershipContracts.Create(start.Compilation, start.Options, start.CancellationToken);
            var helper = new DisposeAnalysisHelper(start.Compilation);
            var inference = new OwnershipInference(start.Compilation, contracts, helper);

            start.RegisterOperationBlockAction(block =>
            {
                if (block.OwningSymbol is not IMethodSymbol method || method.IsImplicitlyDeclared)
                {
                    return;
                }

                // Attribute/default-value roots are not method implementations. Constructor
                // initializers and bodies, however, share the same parameter obligations.
                var operations = block.OperationBlocks
                    .Where(root => root is not (IAttributeOperation or IParameterInitializerOperation))
                    .OrderBy(root => root.Syntax.SpanStart)
                    .SelectMany(EnumerateOperations)
                    .ToImmutableArray();
                if (operations.IsEmpty || operations.Any(o => o is IInvalidOperation))
                {
                    return;
                }

                ReportBorrowedUses(operations, contracts, inference, block);
                var capturedSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
                foreach (var closure in operations.Where(o => o is IAnonymousFunctionOperation or ILocalFunctionOperation))
                {
                    CollectCapturedSymbols(closure, capturedSymbols, block.CancellationToken);
                }

                foreach (var parameter in method.Parameters)
                {
                    if (contracts.AcquiresOwnership(parameter))
                    {
                        var lifetime = new Lifetime(null, parameter, operations, capturedSymbols, contracts, inference, block.CancellationToken);
                        if (lifetime.Analyze() == LifetimeOutcome.Abandoned)
                        {
                            block.ReportDiagnostic(Diagnostic.Create(Rule, parameter.Locations.FirstOrDefault(),
                                parameter.Name, parameter.Type.Name));
                        }
                        else if (lifetime.InvalidUse is { } invalidUse)
                        {
                            block.ReportDiagnostic(invalidUse);
                        }
                    }
                }

                foreach (var operation in operations)
                {
                    block.CancellationToken.ThrowIfCancellationRequested();
                    if (!IsAcquisition(operation, helper, contracts, inference, block.CancellationToken))
                    {
                        continue;
                    }

                    var lifetime = new Lifetime(operation, null, operations, capturedSymbols, contracts, inference, block.CancellationToken);
                    if (lifetime.Analyze() == LifetimeOutcome.Abandoned)
                    {
                        var (location, name) = GetDiagnosticTarget(operation);
                        block.ReportDiagnostic(Diagnostic.Create(Rule, location, name, operation.Type!.Name));
                    }
                    else if (lifetime.InvalidUse is { } invalidUse)
                    {
                        block.ReportDiagnostic(invalidUse);
                    }
                }
            });

            start.RegisterCompilationEndAction(end =>
            {
                foreach (var diagnostic in contracts.Diagnostics)
                {
                    end.ReportDiagnostic(diagnostic);
                }
            });
        });
    }

    private static bool IsAcquisition(IOperation operation, DisposeAnalysisHelper helper,
        OwnershipContracts contracts, OwnershipInference inference, CancellationToken cancellationToken)
    {
        if (!helper.ShouldBeDisposed(operation.Type))
        {
            return false;
        }

        return operation switch
        {
            IObjectCreationOperation => true,
            ITypeParameterObjectCreationOperation => true,
            IInvocationOperation invocation => inference.ReturnsOwnership(invocation.TargetMethod, cancellationToken),
            IConversionOperation { OperatorMethod: { } method } => inference.ReturnsOwnership(method, cancellationToken),
            IPropertyReferenceOperation property => contracts.GetReturnOwnership(property.Property) == OwnershipKind.Owned,
            IAwaitOperation awaited => inference.GetAwaitedMember(awaited.Operation) switch
            {
                IMethodSymbol method => inference.ReturnsOwnership(method, cancellationToken),
                IPropertySymbol property => contracts.GetReturnOwnership(property) == OwnershipKind.Owned,
                _ => false,
            },
            _ => false,
        };
    }

    private static void ReportBorrowedUses(ImmutableArray<IOperation> operations, OwnershipContracts contracts,
        OwnershipInference inference, OperationBlockAnalysisContext context)
    {
        var bindings = new Dictionary<ISymbol, bool>(SymbolEqualityComparer.Default);
        var carrierBindings = new Dictionary<ISymbol, bool>(SymbolEqualityComparer.Default);
        var reported = new HashSet<TextSpan>();
        foreach (var operation in operations)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (GetPatternAlias(operation) is { } patternAlias)
            {
                // A newly declared pattern local has no pre-existing non-borrowed binding to merge.
                TrackBinding(patternAlias.Local, patternAlias.Value, conditional: false);
            }

            foreach (var input in OwnershipInference.GetOperatorInputs(operation))
            {
                if ((IsBorrowedValue(input.Value) || IsBorrowedCarrierValue(input.Value))
                    && inference.AcquiresOwnership(input.Parameter, ImmutableArray<IArgumentOperation>.Empty, context.CancellationToken))
                {
                    Report(input.Value);
                }
            }
            if (operation is IInvocationOperation invocation)
            {
                if (inference.IsDisposeCall(invocation) && IsBorrowedValue(invocation.Instance))
                {
                    Report(invocation.Instance!);
                }

                CheckArguments(invocation.Arguments);
                if (inference.GetInterlockedOwningValue(invocation) is { } value
                    && (IsBorrowedValue(value) || IsBorrowedCarrierValue(value)))
                {
                    Report(value);
                }
            }
            else if (operation is IObjectCreationOperation creation)
            {
                CheckArguments(creation.Arguments);
            }
            else if (operation is IPropertyReferenceOperation propertyReference)
            {
                CheckArguments(propertyReference.Arguments);
            }
            else if (operation is IUsingDeclarationOperation usingDeclaration)
            {
                CheckUsingResources(usingDeclaration.DeclarationGroup);
            }
            else if (operation is IVariableDeclaratorOperation { Initializer: { } initializer } declarator)
            {
                TrackBinding(declarator.Symbol, initializer.Value, conditional: false);
            }
            else if (operation is ISimpleAssignmentOperation assignment)
            {
                if ((IsBorrowedValue(assignment.Value) || IsBorrowedCarrierValue(assignment.Value))
                    && IsOwningDestination(Unwrap(assignment.Target), contracts))
                {
                    Report(assignment.Value);
                }

                if (GetAliasSymbol(assignment.Target) is { } target)
                {
                    TrackBinding(target, assignment.Value, IsConditional(assignment));
                }
            }
            else if (operation is IReturnOperation { ReturnedValue: { } value }
                     && (IsBorrowedValue(value) || IsBorrowedCarrierValue(value))
                     && context.OwningSymbol is IMethodSymbol method
                     && (method.AssociatedSymbol is IPropertySymbol property
                         ? contracts.GetReturnOwnership(property) == OwnershipKind.Owned
                         : inference.ReturnsOwnership(method, context.CancellationToken)))
            {
                Report(value);
            }

            // A using statement captures its resource before its body can reassign the variable.
            if (GetUsingCapture(operation) != null)
            {
                CheckUsingResources(operation);
            }
        }

        bool IsBorrowedValue(IOperation? value) => IsBorrowed(value, contracts, inference, bindings, carrierBindings);
        bool IsBorrowedCarrierValue(IOperation? value) => IsBorrowedCarrier(value, contracts, inference, bindings, carrierBindings);

        void TrackBinding(ISymbol symbol, IOperation value, bool conditional)
        {
            var borrowed = IsBorrowedValue(value);
            var carriedBorrowing = IsBorrowedCarrierValue(value);
            if (conditional)
            {
                borrowed &= bindings.TryGetValue(symbol, out var previous) ? previous : contracts.IsBorrowed(symbol);
                carriedBorrowing &= carrierBindings.TryGetValue(symbol, out var previousCarrier)
                    ? previousCarrier
                    : symbol is IParameterSymbol parameter && inference.IsTaskResultCarrier(parameter.Type)
                        && contracts.IsBorrowed(parameter);
            }

            bindings[symbol] = borrowed;
            carrierBindings[symbol] = carriedBorrowing;
        }

        void CheckUsingResources(IOperation resources)
        {
            if (IsBorrowedValue(resources))
            {
                Report(resources);
                return;
            }

            foreach (var declarator in EnumerateOperations(resources).OfType<IVariableDeclaratorOperation>())
            {
                if (declarator.Initializer is { } initializer
                    && bindings.TryGetValue(declarator.Symbol, out var borrowed) && borrowed)
                {
                    Report(initializer.Value);
                }
            }
        }

        void CheckArguments(ImmutableArray<IArgumentOperation> arguments)
        {
            foreach (var argument in arguments)
            {
                if (argument.Parameter != null
                    && (IsBorrowedValue(argument.Value) || IsBorrowedCarrierValue(argument.Value))
                    && inference.AcquiresOwnership(argument.Parameter, arguments, context.CancellationToken))
                {
                    Report(argument.Value);
                }
            }
        }

        void Report(IOperation value)
        {
            if (reported.Add(value.Syntax.Span))
            {
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.ERP046, value.Syntax.GetLocation(),
                    value.Syntax.ToString(), "is borrowed and must not be disposed or transferred"));
            }
        }
    }

    private static bool IsOwningDestination(IOperation operation, OwnershipContracts contracts)
    {
        return operation switch
        {
            IMemberReferenceOperation member => !contracts.IsBorrowed(member.Member),
            IParameterReferenceOperation parameter => parameter.Parameter.RefKind != RefKind.None
                && !contracts.IsBorrowed(parameter.Parameter),
            _ => false,
        };
    }

    private static bool IsBorrowed(IOperation? operation, OwnershipContracts contracts,
        OwnershipInference inference, IReadOnlyDictionary<ISymbol, bool> bindings,
        IReadOnlyDictionary<ISymbol, bool> carrierBindings)
    {
        if (operation == null)
        {
            return false;
        }

        operation = Unwrap(operation);
        if (GetAliasSymbol(operation) is { } symbol && bindings.TryGetValue(symbol, out var borrowed))
        {
            return borrowed;
        }

        return operation switch
        {
            IParameterReferenceOperation parameter => contracts.IsBorrowed(parameter.Parameter),
            IMemberReferenceOperation member => contracts.IsBorrowed(member.Member)
                || contracts.GetReturnOwnership(member.Member) == OwnershipKind.Borrowed,
            IInvocationOperation invocation when inference.GetConfiguredDisposableResource(invocation) is { } resource =>
                IsBorrowed(resource, contracts, inference, bindings, carrierBindings),
            IInvocationOperation invocation => contracts.GetReturnOwnership(invocation.TargetMethod) == OwnershipKind.Borrowed,
            IConversionOperation { OperatorMethod: { } method } => contracts.GetReturnOwnership(method) == OwnershipKind.Borrowed,
            IConditionalAccessInstanceOperation => IsBorrowed(GetConditionalReceiver(operation), contracts, inference, bindings, carrierBindings),
            IConditionalOperation conditional => IsBorrowed(conditional.WhenTrue, contracts, inference, bindings, carrierBindings)
                && IsBorrowed(conditional.WhenFalse, contracts, inference, bindings, carrierBindings),
            ICoalesceOperation coalesce => IsBorrowed(coalesce.Value, contracts, inference, bindings, carrierBindings)
                && IsBorrowed(coalesce.WhenNull, contracts, inference, bindings, carrierBindings),
            ISimpleAssignmentOperation assignment => IsBorrowed(assignment.Value, contracts, inference, bindings, carrierBindings),
            IAwaitOperation awaited => inference.GetAwaitedMember(awaited.Operation) is { } member
                && contracts.GetReturnOwnership(member) != OwnershipKind.Unspecified
                    ? contracts.GetReturnOwnership(member) == OwnershipKind.Borrowed
                    : IsBorrowedCarrier(awaited.Operation, contracts, inference, bindings, carrierBindings),
            _ => false,
        };
    }

    private static bool IsBorrowedCarrier(IOperation? operation, OwnershipContracts contracts,
        OwnershipInference inference, IReadOnlyDictionary<ISymbol, bool> bindings,
        IReadOnlyDictionary<ISymbol, bool> carrierBindings)
    {
        if (operation == null)
        {
            return false;
        }

        operation = Unwrap(operation);
        if (GetAliasSymbol(operation) is { } symbol && carrierBindings.TryGetValue(symbol, out var borrowed))
        {
            return borrowed;
        }

        if (inference.GetCompletedTaskValue(operation) is { } value)
        {
            return IsBorrowed(value, contracts, inference, bindings, carrierBindings);
        }

        if (inference.GetConfiguredTask(operation) is { } task)
        {
            return IsBorrowedCarrier(task, contracts, inference, bindings, carrierBindings);
        }

        if (operation is IInvocationOperation invocation && inference.GetFluentReceiver(invocation) is { } receiver)
        {
            return IsBorrowedCarrier(receiver, contracts, inference, bindings, carrierBindings);
        }

        return operation switch
        {
            IParameterReferenceOperation parameter => inference.IsTaskResultCarrier(parameter.Type)
                && contracts.IsBorrowed(parameter.Parameter),
            ISimpleAssignmentOperation assignment => IsBorrowedCarrier(assignment.Value, contracts, inference, bindings, carrierBindings),
            IConditionalOperation conditional => IsBorrowedCarrier(conditional.WhenTrue, contracts, inference, bindings, carrierBindings)
                && IsBorrowedCarrier(conditional.WhenFalse, contracts, inference, bindings, carrierBindings),
            ICoalesceOperation coalesce => IsBorrowedCarrier(coalesce.Value, contracts, inference, bindings, carrierBindings)
                && IsBorrowedCarrier(coalesce.WhenNull, contracts, inference, bindings, carrierBindings),
            _ => false,
        };
    }

    internal static IOperation? GetConditionalReceiver(IOperation operation)
    {
        for (var parent = operation.Parent; parent != null; parent = parent.Parent)
        {
            if (parent is IConditionalAccessOperation conditional)
            {
                return conditional.Operation;
            }
        }

        return null;
    }

    private static (Location Location, string Name) GetDiagnosticTarget(IOperation operation)
    {
        for (var parent = operation.Parent; parent != null; parent = parent.Parent)
        {
            if (parent is IVariableDeclaratorOperation declarator
                && declarator.Syntax is VariableDeclaratorSyntax syntax)
            {
                return (syntax.Identifier.GetLocation(), declarator.Symbol.Name);
            }

            if (parent is ISimpleAssignmentOperation { Target: ILocalReferenceOperation local })
            {
                return (local.Syntax.GetLocation(), local.Local.Name);
            }

            if (parent is IConversionOperation && ReferenceEquals(Unwrap(parent), parent)
                || parent is not (IConversionOperation or IParenthesizedOperation or IAwaitOperation or IVariableInitializerOperation))
            {
                break;
            }
        }

        return (operation.Syntax.GetLocation(), operation.Syntax.ToString());
    }

    internal static IEnumerable<IOperation> EnumerateOperations(IOperation root)
    {
        // Postorder follows evaluation order for arguments, initializers and their containing call.
        foreach (var child in root.ChildOperations)
        {
            if (child is INameOfOperation)
            {
                continue;
            }

            if (child is IAnonymousFunctionOperation or ILocalFunctionOperation)
            {
                // Captures can end local certainty, but nested bodies are separate lifetimes.
                yield return child;
                continue;
            }

            foreach (var operation in EnumerateOperations(child))
            {
                yield return operation;
            }
        }

        yield return root;
    }

    private static void CollectCapturedSymbols(IOperation operation, HashSet<ISymbol> symbols, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (operation is INameOfOperation)
        {
            return;
        }

        if (operation is ILocalReferenceOperation or IParameterReferenceOperation
            && GetAliasSymbol(operation) is { } symbol)
        {
            symbols.Add(symbol);
        }

        foreach (var child in operation.ChildOperations)
        {
            CollectCapturedSymbols(child, symbols, cancellationToken);
        }
    }

    internal static IOperation Unwrap(IOperation operation)
    {
        while (true)
        {
            switch (operation)
            {
                case IConversionOperation conversion when conversion.Conversion.Exists
                    && !conversion.Conversion.IsUserDefined
                    && conversion.Type?.TypeKind != TypeKind.Dynamic
                    && conversion.Operand.Type?.TypeKind != TypeKind.Dynamic:
                    operation = conversion.Operand;
                    break;
                case IParenthesizedOperation parenthesized:
                    operation = parenthesized.Operand;
                    break;
                default:
                    return operation;
            }
        }
    }

    internal static IUsingOperation? GetUsingCapture(IOperation operation)
    {
        return operation.Parent is IUsingOperation usingOperation && ReferenceEquals(usingOperation.Resources, operation)
            ? usingOperation : null;
    }

    internal static ISymbol? GetAliasSymbol(IOperation operation)
    {
        return Unwrap(operation) switch
        {
            ILocalReferenceOperation local => local.Local,
            IParameterReferenceOperation parameter => parameter.Parameter,
            _ => null,
        };
    }

    internal static (IOperation Value, ILocalSymbol Local)? GetPatternAlias(IOperation operation)
    {
        if (operation is not IIsPatternOperation isPattern)
        {
            return null;
        }

        var pattern = isPattern.Pattern;
        while (pattern is INegatedPatternOperation negated)
        {
            pattern = negated.Pattern;
        }

        // Only a direct type-pattern binding aliases the input, not nested property patterns.
        return pattern is IDeclarationPatternOperation { DeclaredSymbol: ILocalSymbol local }
            ? (isPattern.Value, local) : null;
    }

    internal static bool IsConditional(IOperation operation)
    {
        for (var parent = operation.Parent; parent != null; parent = parent.Parent)
        {
            if (parent is IConditionalOperation or IConditionalAccessOperation or ICoalesceOperation
                or ISwitchOperation or ILoopOperation or ICatchClauseOperation
                or IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr })
            {
                return true;
            }
        }

        return false;
    }

    private enum LifetimeOutcome
    {
        Abandoned,
        Discharged,
        Unknown,
    }

    private sealed class Lifetime
    {
        private readonly IOperation? _creation;
        private readonly ImmutableArray<IOperation> _operations;
        private readonly HashSet<ISymbol> _capturedSymbols;
        private readonly OwnershipContracts _contracts;
        private readonly OwnershipInference _inference;
        private readonly CancellationToken _cancellationToken;
        private readonly HashSet<ISymbol> _aliases = new(SymbolEqualityComparer.Default);
        private readonly HashSet<ISymbol> _uncertainAliases = new(SymbolEqualityComparer.Default);
        private readonly HashSet<ISymbol> _carriers = new(SymbolEqualityComparer.Default);
        private readonly HashSet<ISymbol> _uncertainCarriers = new(SymbolEqualityComparer.Default);

        public Diagnostic? InvalidUse { get; private set; }

        public Lifetime(IOperation? creation, IParameterSymbol? parameter, ImmutableArray<IOperation> operations,
            HashSet<ISymbol> capturedSymbols,
            OwnershipContracts contracts, OwnershipInference inference, CancellationToken cancellationToken)
        {
            _creation = creation;
            _operations = operations;
            _capturedSymbols = capturedSymbols;
            _contracts = contracts;
            _inference = inference;
            _cancellationToken = cancellationToken;
            if (parameter != null)
            {
                // An acquiring Task<T>/ValueTask<T> parameter owns its result, not wrapper cleanup.
                (inference.IsTaskResultCarrier(parameter.Type) ? _carriers : _aliases).Add(parameter);
            }
        }

        public LifetimeOutcome Analyze()
        {
            var started = _creation == null;
            var ownershipUnknown = _capturedSymbols.Overlaps(_aliases) || _capturedSymbols.Overlaps(_carriers);
            foreach (var operation in _operations)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                if (!started)
                {
                    started = ReferenceEquals(operation, _creation);
                    if (!started)
                    {
                        continue;
                    }
                }

                if (GetPatternAlias(operation) is { } patternAlias)
                {
                    AssignAlias(patternAlias.Local, patternAlias.Value, operation);
                }

                foreach (var input in OwnershipInference.GetOperatorInputs(operation))
                {
                    if ((Matches(input.Value) || Carries(input.Value))
                        && _inference.AcquiresOwnership(input.Parameter, ImmutableArray<IArgumentOperation>.Empty, _cancellationToken))
                    {
                        return Discharge(operation, Matches(input.Value, requireDefinite: true)
                            || Carries(input.Value, requireDefinite: true));
                    }
                    if ((Matches(input.Value) || Carries(input.Value)) && !_contracts.IsBorrowed(input.Parameter))
                    {
                        ownershipUnknown = true;
                    }
                }

                switch (operation)
                {
                    case IVariableDeclaratorOperation { Initializer: { } initializer } declarator:
                        AssignAlias(declarator.Symbol, initializer.Value, operation);
                        break;
                    case ISimpleAssignmentOperation assignment:
                        if (Matches(assignment.Value) || Carries(assignment.Value))
                        {
                            switch (Unwrap(assignment.Target))
                            {
                                case ILocalReferenceOperation local:
                                    AssignAlias(local.Local, assignment.Value, operation);
                                    break;
                                case IParameterReferenceOperation parameter when IsOwningDestination(parameter, _contracts):
                                    return Discharge(operation, Matches(assignment.Value, requireDefinite: true)
                                        || Carries(assignment.Value, requireDefinite: true));
                                case IParameterReferenceOperation parameter:
                                    AssignAlias(parameter.Parameter, assignment.Value, operation);
                                    break;
                                case IMemberReferenceOperation member when !_contracts.IsBorrowed(member.Member):
                                    return Discharge(operation, Matches(assignment.Value, requireDefinite: true)
                                        || Carries(assignment.Value, requireDefinite: true));
                            }
                        }
                        else if (GetAliasSymbol(assignment.Target) is { } alias)
                        {
                            AssignAlias(alias, assignment.Value, operation);
                        }
                        break;
                    case IInvocationOperation invocation:
                        if (_inference.IsDisposeCall(invocation) && Matches(invocation.Instance))
                        {
                            return Discharge(operation, Matches(invocation.Instance, requireDefinite: true));
                        }

                        var published = _inference.GetInterlockedOwningValue(invocation);
                        if (MovesArgument(invocation.Arguments) || Matches(published) || Carries(published))
                        {
                            return Discharge(operation, MovesArgument(invocation.Arguments, requireDefinite: true)
                                || Matches(published, requireDefinite: true) || Carries(published, requireDefinite: true));
                        }
                        if (_inference.GetCompletedTaskValue(invocation) == null
                            && Escapes(invocation.Arguments, _inference.GetFluentReceiver(invocation, _cancellationToken)))
                        {
                            ownershipUnknown = true;
                        }
                        break;
                    case IObjectCreationOperation creation when MovesArgument(creation.Arguments):
                        return Discharge(operation, MovesArgument(creation.Arguments, requireDefinite: true));
                    case IObjectCreationOperation creation when _inference.GetCompletedTaskValue(creation) == null
                        && Escapes(creation.Arguments):
                        ownershipUnknown = true;
                        break;
                    case IPropertyReferenceOperation property when MovesArgument(property.Arguments):
                        return Discharge(operation, MovesArgument(property.Arguments, requireDefinite: true));
                    case IPropertyReferenceOperation property when Escapes(property.Arguments):
                        ownershipUnknown = true;
                        break;
                    case IReturnOperation returned when Matches(returned.ReturnedValue) || Carries(returned.ReturnedValue):
                        return LifetimeOutcome.Discharged;
                    case IDelegateCreationOperation { Target: IMethodReferenceOperation method } when Matches(method.Instance):
                        ownershipUnknown = true;
                        break;
                    case IDynamicInvocationOperation dynamicInvocation
                        when dynamicInvocation.Arguments.Any(a => Matches(a) || Carries(a)):
                        ownershipUnknown = true;
                        break;
                    case IDynamicObjectCreationOperation dynamicCreation
                        when dynamicCreation.Arguments.Any(a => Matches(a) || Carries(a)):
                        ownershipUnknown = true;
                        break;
                    case IDynamicIndexerAccessOperation dynamicIndexer
                        when dynamicIndexer.Arguments.Any(a => Matches(a) || Carries(a)):
                        ownershipUnknown = true;
                        break;
                    case IUsingDeclarationOperation declaration when declaration.DeclarationGroup.Declarations
                        .SelectMany(d => d.Declarators).Any(d => _aliases.Contains(d.Symbol)):
                        return LifetimeOutcome.Discharged;
                }

                // Closures capture variables, including resources assigned after closure creation.
                ownershipUnknown |= _capturedSymbols.Overlaps(_aliases) || _capturedSymbols.Overlaps(_carriers);

                if (GetUsingCapture(operation) is { } captured
                    && (Matches(operation) || captured.Locals.Any(l => _aliases.Contains(l))))
                {
                    return LifetimeOutcome.Discharged;
                }
            }

            return ownershipUnknown ? LifetimeOutcome.Unknown : LifetimeOutcome.Abandoned;
        }

        private LifetimeOutcome Discharge(IOperation terminal, bool definite)
        {
            // Restrict invalid-use reports to later statements in the very same lexical block.
            // Conditional cleanup, finally blocks and implicit using cleanup remain best effort.
            var statement = terminal.Syntax.FirstAncestorOrSelf<StatementSyntax>();
            if (!definite || statement?.Parent is not BlockSyntax block
                || IsConditional(terminal)
                || EnumerateOperations(terminal).Any(o => o is IConditionalOperation or ICoalesceOperation))
            {
                return LifetimeOutcome.Discharged;
            }

            var passedTerminal = false;
            foreach (var operation in _operations)
            {
                if (!passedTerminal)
                {
                    passedTerminal = ReferenceEquals(operation, terminal);
                    continue;
                }

                // The enclosing assignment may replace an alias in the terminal statement itself.
                if (operation is ISimpleAssignmentOperation assignment
                    && GetAliasSymbol(assignment.Target) is { } target)
                {
                    AssignAlias(target, assignment.Value, operation);
                }
                else if (operation is IVariableDeclaratorOperation { Initializer: { } initializer } declarator)
                {
                    AssignAlias(declarator.Symbol, initializer.Value, operation);
                }

                if (operation.Syntax.SpanStart < statement.Span.End
                    || operation is not (ILocalReferenceOperation or IParameterReferenceOperation or IAwaitOperation)
                    || !Matches(operation, requireDefinite: true)
                    || IsConditional(operation)
                    || operation.Syntax.FirstAncestorOrSelf<StatementSyntax>()?.Parent != block)
                {
                    continue;
                }

                if (operation.Parent is ISimpleAssignmentOperation { Target: var assignmentTarget }
                    && ReferenceEquals(assignmentTarget, operation))
                {
                    continue;
                }

                InvalidUse = Diagnostic.Create(DiagnosticDescriptors.ERP046, operation.Syntax.GetLocation(),
                    operation.Syntax.ToString(), "is used after disposal or ownership transfer");
                break;
            }

            return LifetimeOutcome.Discharged;
        }

        private void AssignAlias(ISymbol symbol, IOperation value, IOperation assignment)
        {
            var matches = Matches(value);
            var carries = Carries(value);
            var definiteMatch = Matches(value, requireDefinite: true);
            var definiteCarrier = Carries(value, requireDefinite: true);
            Track(_aliases, _uncertainAliases, matches, definiteMatch);
            Track(_carriers, _uncertainCarriers, carries, definiteCarrier);

            void Track(HashSet<ISymbol> aliases, HashSet<ISymbol> uncertain, bool matchesValue, bool definite)
            {
                if (matchesValue)
                {
                    aliases.Add(symbol);
                    if (definite && !IsConditional(assignment))
                    {
                        uncertain.Remove(symbol);
                    }
                    else
                    {
                        uncertain.Add(symbol);
                    }
                }
                else if (IsConditional(assignment) && aliases.Contains(symbol))
                {
                    uncertain.Add(symbol);
                }
                else
                {
                    aliases.Remove(symbol);
                    uncertain.Remove(symbol);
                }
            }
        }

        private bool Escapes(ImmutableArray<IArgumentOperation> arguments, IOperation? preservedReceiver = null)
        {
            return arguments.Any(argument => (Matches(argument.Value) || Carries(argument.Value))
                && !ReferenceEquals(argument.Value, preservedReceiver)
                && _inference.IsUnknownArgument(argument, arguments));
        }

        private bool Carries(IOperation? value, bool requireDefinite = false)
        {
            if (value == null)
            {
                return false;
            }

            value = Unwrap(value);
            if (GetAliasSymbol(value) is { } symbol)
            {
                return _carriers.Contains(symbol) && (!requireDefinite || !_uncertainCarriers.Contains(symbol));
            }

            if (_inference.GetCompletedTaskValue(value) is { } result)
            {
                return Matches(result, requireDefinite);
            }

            if (_inference.GetConfiguredTask(value) is { } task)
            {
                return Carries(task, requireDefinite);
            }

            if (value is IInvocationOperation invocation
                && _inference.GetFluentReceiver(invocation, _cancellationToken) is { } receiver)
            {
                return Carries(receiver, requireDefinite);
            }

            return value switch
            {
                ISimpleAssignmentOperation assignment => Carries(assignment.Value, requireDefinite),
                IConditionalOperation conditional => requireDefinite
                    ? Carries(conditional.WhenTrue, true) && Carries(conditional.WhenFalse, true)
                    : Carries(conditional.WhenTrue) || Carries(conditional.WhenFalse),
                ICoalesceOperation coalesce => requireDefinite
                    ? Carries(coalesce.Value, true) && Carries(coalesce.WhenNull, true)
                    : Carries(coalesce.Value) || Carries(coalesce.WhenNull),
                _ => false,
            };
        }

        private bool MovesArgument(ImmutableArray<IArgumentOperation> arguments, bool requireDefinite = false)
        {
            foreach (var argument in arguments)
            {
                if (argument.Parameter != null
                    && (Matches(argument.Value, requireDefinite) || Carries(argument.Value, requireDefinite))
                    && _inference.AcquiresOwnership(argument.Parameter, arguments, _cancellationToken))
                {
                    return true;
                }
            }

            return false;
        }

        private bool Matches(IOperation? value, bool requireDefinite = false)
        {
            if (value == null)
            {
                return false;
            }

            value = Unwrap(value);
            if (ReferenceEquals(value, _creation))
            {
                return true;
            }

            switch (value)
            {
                case ILocalReferenceOperation local:
                    return _aliases.Contains(local.Local) && (!requireDefinite || !_uncertainAliases.Contains(local.Local));
                case IParameterReferenceOperation parameter:
                    return _aliases.Contains(parameter.Parameter) && (!requireDefinite || !_uncertainAliases.Contains(parameter.Parameter));
                case IConditionalAccessInstanceOperation:
                    return Matches(GetConditionalReceiver(value), requireDefinite);
                case IConditionalOperation conditional:
                    return requireDefinite
                        ? Matches(conditional.WhenTrue, true) && Matches(conditional.WhenFalse, true)
                        : Matches(conditional.WhenTrue) || Matches(conditional.WhenFalse);
                case ICoalesceOperation coalesce:
                    return requireDefinite
                        ? Matches(coalesce.Value, true) && Matches(coalesce.WhenNull, true)
                        : Matches(coalesce.Value) || Matches(coalesce.WhenNull);
                case ISimpleAssignmentOperation assignment:
                    return Matches(assignment.Value, requireDefinite);
                case IAwaitOperation awaited:
                    return Carries(awaited.Operation, requireDefinite);
                case IInvocationOperation invocation when _inference.GetConfiguredDisposableResource(invocation) is { } resource:
                    return Matches(resource, requireDefinite);
                case IInvocationOperation invocation when _inference.GetFluentReceiver(invocation, _cancellationToken) is { } receiver:
                    return Matches(receiver, requireDefinite);
            }

            return false;
        }
    }
}

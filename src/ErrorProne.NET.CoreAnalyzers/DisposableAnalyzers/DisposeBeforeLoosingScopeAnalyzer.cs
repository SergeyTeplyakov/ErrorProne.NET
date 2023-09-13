using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.ContractsLight;
using System.Linq;
using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using ErrorProne.NET.Core;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;
using System.Data.Common;
using ErrorProne.NET.Extensions;

namespace ErrorProne.NET.DisposableAnalyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DisposeBeforeLoosingScopeAnalyzer : DiagnosticAnalyzerBase
{
    /// <nodoc />
    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptors.ERP041;

    public override bool ReportDiagnosticsOnGeneratedCode { get; } = false;

    /// <nodoc />
    public DisposeBeforeLoosingScopeAnalyzer()
        : base(Rule)
    {
    }

    /// <inheritdoc />
    protected override void InitializeCore(AnalysisContext context)
    {
        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
        context.RegisterOperationAction(AnalyzeMethodCall, OperationKind.Invocation);
        
        context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);

        context.RegisterOperationAction(AnalyzeMethodBody, OperationKind.MethodBody);

    }

    /// <summary>
    /// Analyzes method implementations for method that acquiring ownership via one of the arguments.
    /// </summary>
    private void AnalyzeMethodBody(OperationAnalysisContext context)
    {
        if (context.ContainingSymbol is not IMethodSymbol method 
            || !HasAcquiresOwnershipParameters(method, out var parameter) 
            // Skipping methods marked with 'KeepOwnership' attribute.
            || method.HasAttributeWithName(DisposableAttributes.KeepsOwnershipAttribute))
        {
            return;
        }

        var syntax = (ParameterSyntax)parameter.DeclaringSyntaxReferences[0].GetSyntax();

        if (!context.Operation.HasAnyOperationDescendant(o => o.Kind == OperationKind.ParameterReference, out var operation))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.Identifier.GetLocation(), parameter.Name));
            return;
        }

        // It seems that the parameter was used. Checking how.
        var cfg = context.GetControlFlowGraph();

        if (IsDisposed(parameter, cfg))
        {
            // Parameter is disposed. We're good!
            return;
        }

        if (OwnershipIsMoved(LocalOrParameterReference.Create(parameter), cfg))
        {
            // The ownership is moved again.
            return;
        }

        if (HasParent<IUsingOperation>(operation))
        {
            // We have 'using(param) {} '
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.Identifier.GetLocation(), parameter.Name));
    }

    private static bool HasAcquiresOwnershipParameters(IMethodSymbol method, [NotNullWhen(true)]out IParameterSymbol? result)
    {
        result = null;
        foreach (var p in method.Parameters)
        {
            if (p.HasAttributeWithName(DisposableAttributes.AcquiresOwnershipAttribute))
            {
                result = p;
                return true;
            }
        }

        return false;
    }

    private void AnalyzePropertyReference(OperationAnalysisContext context)
    {
        var propertyReference = (IPropertyReferenceOperation)context.Operation;

        var helper = new DisposeAnalysisHelper(context.Compilation);
        if (!ReleasesOwnership(propertyReference.Property, helper))
        {
            // We're not calling a factory method, so we're not taking an ownership of the object.
            return;
        }

        AnalyzeCore(context, propertyReference.Type, propertyReference);
    }
    
    private void AnalyzeMethodCall(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;

        var helper = new DisposeAnalysisHelper(context.Compilation);
        if (!ReleasesOwnership(invocation.TargetMethod, helper))
        {
            // We're not calling a factory method, so we're not taking an ownership of the object.
            return;
        }

        AnalyzeCore(context, invocation.Type, invocation);
    }

    private void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        var objectCreation = (IObjectCreationOperation)context.Operation;
        AnalyzeCore(context, objectCreation.Type, objectCreation);
    }

    private void AnalyzeCore(OperationAnalysisContext context, ITypeSymbol expressionType, IOperation operation)
    {
        if (operation.Parent is IFieldInitializerOperation or IPropertyInitializerOperation)
        {
            return;
        }

        var helper = new DisposeAnalysisHelper(context.Compilation);

        if (helper.ShouldBeDisposed(expressionType))
        {
            // The type is disposable, but its possible that the operation points not to a factory method.
            // Meaning that we're not taking an ownership of the object.
            
            // The operation might be a variable declaration.
            // Trying to figure it out since we have a ton of logic relying on that.
            var variableDeclaration = EnumerateParents(operation).OfType<IVariableDeclaratorOperation>().FirstOrDefault();

            var controlFlowGraph = context.GetControlFlowGraph();
            if (IsDisposedOrOwnershipIsMoved(variableDeclaration, operation, controlFlowGraph))
            {
                return;
            }

            
            // Analyzing body of the method to see if all the arguments are disposed.
            if (IsFactoryMethodLikeSignature(context.ContainingSymbol, helper) && IsFactoryMethodImpl(operation, variableDeclaration, controlFlowGraph))
            {
                // This is factory method/property so we can't dispose an argument since we're returning it.
                return;
            }

            if (variableDeclaration != null)
            {
                // Checking if the parent is 'using' declaration or 'using' statement, then we're good

                if (HasParent<IUsingDeclarationOperation>(variableDeclaration) ||
                    HasParent<IUsingOperation>(variableDeclaration))
                {
                    // Current object creation is a part of 'using' declaration.
                    return;
                }

                if (variableDeclaration.Syntax is VariableDeclaratorSyntax vds)
                {
                    var identifierLocation = vds.Identifier.GetLocation();
                    context.ReportDiagnostic(Diagnostic.Create(Rule, identifierLocation, variableDeclaration.Symbol.Name));
                    return;
                }

                Contract.Assert(false, "Should not get here!");
            }

            // Variable declaration is null
            if (HasParent<IUsingOperation>(operation))
            {
                // We're good, this is 'using(new Disposable())' statement
                return;
            }

            var location = operation.Syntax.GetLocation();
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, operation.Syntax.ToString()));
        }
    }

    /// <summary>
    /// Method returns true if a variable's ownership is moved to another place (method).
    /// </summary>
    private bool OwnershipIsMoved(LocalOrParameterReference localOrParam, ControlFlowGraph cfg)
    {
        return OwnershipIsMoved(cfg, argumentMatches: a => localOrParam.IsReferenced(a.Value));
    }
    
    /// <summary>
    /// Method returns true if a variable's ownership is moved to another place (method).
    /// </summary>
    private bool OwnershipIsMoved(ControlFlowGraph cfg, Func<IArgumentOperation, bool> argumentMatches)
    {
        foreach (var invocation in cfg.DescendantOperations().OfType<IInvocationOperation>())
        {
            for (int i = 0; i < invocation.Arguments.Length; i++)
            {
                var a = invocation.Arguments[i];

                if (argumentMatches(a))
                {
                    var p = invocation.TargetMethod.Parameters[i];
                    if (p.HasAttributeWithName(DisposableAttributes.AcquiresOwnershipAttribute))
                    {
                        return true;
                    }
                }
            }
        }
        
        return false;
    }



    // TODO: move to another place.
    

    private static bool IsLocal(IVariableDeclaratorOperation variableDeclaration, IOperation operation)
    {
        return operation is ILocalReferenceOperation lro &&
               lro.Local.Equals(variableDeclaration.Symbol, SymbolEqualityComparer.Default);
    }

    // 
    public readonly record struct LocalOrParameterReference
    {
        private readonly IVariableDeclaratorOperation? _variableDeclaration;
        private readonly IParameterSymbol? _parameter;

        public LocalOrParameterReference(IVariableDeclaratorOperation variableDeclaration)
        {
            _variableDeclaration = variableDeclaration;
        }

        public LocalOrParameterReference(IParameterSymbol parameter)
        {
            _parameter = parameter;
        }

        public static LocalOrParameterReference Create(IVariableDeclaratorOperation variableDeclaration) => new (variableDeclaration);
        public static LocalOrParameterReference Create(IParameterSymbol parameter) => new (parameter);

        public bool IsReferenced(IOperation operation)
        {
            if (operation is ILocalReferenceOperation lro && _variableDeclaration is not null)
            {
                return lro.Local.Equals(_variableDeclaration.Symbol, SymbolEqualityComparer.Default);
            }
            else if (operation is IParameterReferenceOperation pro && _parameter is not null)
            {
                return pro.Parameter.Equals(_parameter, SymbolEqualityComparer.Default);
            }

            return false;
        }
    }

    /// <summary>
    /// Returns true when a given <paramref name="methodOrProperty"/> looks like a factory method from a return type perspective.
    /// </summary>
    private bool IsFactoryMethodLikeSignature(ISymbol methodOrProperty, DisposeAnalysisHelper helper)
    {
        if (!helper.IsDisposable(TryFindReturnType(methodOrProperty)))
        {
            return false;
        }

        // This is not a factory if it keeps the ownership. It might be, but we're not going to dispose the results.
        if (methodOrProperty.HasAttributeWithName(DisposableAttributes.KeepsOwnershipAttribute))
        {
            return false;
        }

        // Special case for methods
        // If there is an argument, marked with `AcquiresOwnershipAttribute`, then it's not a factory method.
        if (methodOrProperty is IMethodSymbol ms && HasAcquiresOwnershipParameters(ms, out _))
        {
            return false;
        }

        return true;
    }
    
    /// <summary>
    /// Returns true when a given <paramref name="methodOrProperty"/> looks like a factory method and probably returns an ownership.
    /// </summary>
    private bool ReleasesOwnership(ISymbol methodOrProperty, DisposeAnalysisHelper helper)
    {
        if (!helper.IsDisposable(TryFindReturnType(methodOrProperty)))
        {
            return false;
        }

        if (methodOrProperty.HasAttributeWithName(DisposableAttributes.KeepsOwnershipAttribute))
        {
            return false;
        }
        
        if (methodOrProperty.HasAttributeWithName(DisposableAttributes.ReleasesOwnershipAttribute))
        {
            return true;
        }

        // By default properties do not release ownership unless they are marked with `ReleasesOwnershipAttribute`.
        if (methodOrProperty is IPropertySymbol)
        {
            return false;
        }

        // Special case for methods
        // If there is an argument, marked with `AcquiresOwnershipAttribute`, then it's not a factory method.
        if (methodOrProperty is IMethodSymbol ms && HasAcquiresOwnershipParameters(ms, out _))
        {
            return false;
        }

        return true;
    }

    private bool IsFactoryMethodImpl(IOperation operation, IVariableDeclaratorOperation? variableDeclaration, ControlFlowGraph cfg)
    {
        if (variableDeclaration is null && HasParent<IReturnOperation>(operation))
        {
            return true;
        }

        if (variableDeclaration is null)
        {
            return false;
        }

        var localVariable = variableDeclaration.Symbol;
        var branches = cfg.GetExit().Predecessors.Select(b => new BranchWithInfo(b)).ToArray();
        
        // We're fine if all the branches are returns and the return value is the local variable.
        if (branches.All(b =>
                b.Kind == ControlFlowBranchSemantics.Return && b.BranchValue is ILocalReferenceOperation lro &&
                lro.Local.Equals(localVariable, SymbolEqualityComparer.Default)))
        {
            return true;
        }


        return false;
    }

    // TODO: move this to helpers
    private static ITypeSymbol? TryFindReturnType(ISymbol operation)
    {
        if (operation is IMethodSymbol ms && ms.MethodKind != MethodKind.Constructor)
        {
            return ms.ReturnType;
        }
        else if (operation is IPropertySymbol ps)
        {
            return ps.Type;
        }

        return null;
    }

    private bool IsDisposedOrOwnershipIsMoved(IVariableDeclaratorOperation? variableDeclaration, IOperation operation, ControlFlowGraph cfg)
    {
        if (variableDeclaration != null)
        {
            if (IsDisposed(variableDeclaration.Symbol, cfg) ||
                OwnershipIsMoved(LocalOrParameterReference.Create(variableDeclaration), cfg))
            {
                return true;
            }
        }

        // It is possible that the ownership is moved, but the variable is not null,
        // like 'new X().ReleaseOwnership()'.
        if (OwnershipIsMoved(cfg,
                // Using syntax node for comparison. Which is weird, since we'll get here
                // two different object creation operations.
                a => { return operation.Syntax.IsEquivalentTo(a.Value.Syntax); }))
        {
            return true;
        }

        // Checking if the ownership is moved.
        return false;
    }
    
    private bool IsDisposed(ISymbol localVariable, ControlFlowGraph cfg)
    {
        var branches = cfg.GetExit().Predecessors.Select(b => new BranchWithInfo(b)).ToArray();

        // Using a naive approach for now: if the variable is disposed in finally
        // or in both: try and catch blocks, then we're good.
        bool disposedInTry = false;
        bool disposedInCatch = false;
        foreach (var block in cfg.Blocks)
        {
            if (block.IsReachable && block.Kind == BasicBlockKind.Block &&
                block.ConditionKind == ControlFlowConditionKind.None && DisposedUnconditionallyIn(block, localVariable))
            {
                // We have an unconditional Dispose in one of the blocks.
                return true;
            }

            if (block.IsFinallyBlock() && DisposedUnconditionallyIn(block, localVariable))
            {
                return true;
            }

            if (block.IsTryBlock() && DisposedUnconditionallyIn(block, localVariable))
            {
                disposedInTry = true;

                if (disposedInCatch)
                {
                    return true;
                }
            }

            if (block.IsCatchBlock() && DisposedUnconditionallyIn(block, localVariable))
            {
                disposedInCatch = true;

                if (disposedInTry)
                {
                    return true;
                }
            }
        }
        
        return false;
    }

    private bool DisposedUnconditionallyIn(BasicBlock block, ISymbol localVariable)
    {
        var invocations = block.DescendantOperations().OfType<IInvocationOperation>();
        foreach (var invocation in invocations)
        {
            if ((invocation.Instance is ILocalReferenceOperation lro && lro.Local.Equals(localVariable, SymbolEqualityComparer.Default)) ||
                (invocation.Instance is IParameterReferenceOperation pro && pro.Parameter.Equals(localVariable, SymbolEqualityComparer.Default)))
            {
                // TODO: cover if blocks.
                if (invocation.TargetMethod.Name == "Dispose")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasParent<T>(IOperation operation) where T: IOperation
    {
        return EnumerateParents(operation).OfType<T>().FirstOrDefault() != null;
    }

    private static IEnumerable<IOperation> EnumerateParents(IOperation? operation)
    {
        while (operation != null)
        {
            operation = operation.Parent;
            yield return operation;
        }
    }
}

/// <summary>
/// Contains aggregated information about a control flow branch.
/// </summary>
/// TODO: move to a common place.
public sealed class BranchWithInfo
{
    private static readonly Func<ControlFlowRegion, IEnumerable<ControlFlowRegion>> s_getTransitiveNestedRegions = GetTransitiveNestedRegions;

    internal BranchWithInfo(ControlFlowBranch branch)
        : this(branch.Destination!, branch.EnteringRegions, branch.LeavingRegions, branch.FinallyRegions,
              branch.Semantics, branch.Source.BranchValue,
              GetControlFlowConditionKind(branch),
              leavingRegionLocals: ComputeLeavingRegionLocals(branch.LeavingRegions),
              leavingRegionFlowCaptures: ComputeLeavingRegionFlowCaptures(branch.LeavingRegions))
    {
    }

    internal BranchWithInfo(BasicBlock destination)
        : this(destination,
              enteringRegions: ImmutableArray<ControlFlowRegion>.Empty,
              leavingRegions: ImmutableArray<ControlFlowRegion>.Empty,
              finallyRegions: ImmutableArray<ControlFlowRegion>.Empty,
              kind: ControlFlowBranchSemantics.Regular,
              branchValue: null,
              controlFlowConditionKind: ControlFlowConditionKind.None,
              leavingRegionLocals: ImmutableHashSet<ILocalSymbol>.Empty,
              leavingRegionFlowCaptures: ImmutableHashSet<CaptureId>.Empty)
    {
    }

    private BranchWithInfo(
        BasicBlock destination,
        ImmutableArray<ControlFlowRegion> enteringRegions,
        ImmutableArray<ControlFlowRegion> leavingRegions,
        ImmutableArray<ControlFlowRegion> finallyRegions,
        ControlFlowBranchSemantics kind,
        IOperation? branchValue,
        ControlFlowConditionKind controlFlowConditionKind,
        IEnumerable<ILocalSymbol> leavingRegionLocals,
        IEnumerable<CaptureId> leavingRegionFlowCaptures)
    {
        Destination = destination;
        Kind = kind;
        EnteringRegions = enteringRegions;
        LeavingRegions = leavingRegions;
        FinallyRegions = finallyRegions;
        BranchValue = branchValue;
        ControlFlowConditionKind = controlFlowConditionKind;
        LeavingRegionLocals = leavingRegionLocals;
        LeavingRegionFlowCaptures = leavingRegionFlowCaptures;
    }

    public BasicBlock Destination { get; }
    public ControlFlowBranchSemantics Kind { get; }
    public ImmutableArray<ControlFlowRegion> EnteringRegions { get; }
    public ImmutableArray<ControlFlowRegion> FinallyRegions { get; }
    public ImmutableArray<ControlFlowRegion> LeavingRegions { get; }
    public IOperation? BranchValue { get; }

    public ControlFlowConditionKind ControlFlowConditionKind { get; }

    public IEnumerable<ILocalSymbol> LeavingRegionLocals { get; }
    public IEnumerable<CaptureId> LeavingRegionFlowCaptures { get; }

    internal BranchWithInfo WithEmptyRegions(BasicBlock destination)
    {
        return new BranchWithInfo(
            destination,
            enteringRegions: ImmutableArray<ControlFlowRegion>.Empty,
            leavingRegions: ImmutableArray<ControlFlowRegion>.Empty,
            finallyRegions: ImmutableArray<ControlFlowRegion>.Empty,
            kind: Kind,
            branchValue: BranchValue,
            controlFlowConditionKind: ControlFlowConditionKind,
            leavingRegionLocals: ImmutableHashSet<ILocalSymbol>.Empty,
            leavingRegionFlowCaptures: ImmutableHashSet<CaptureId>.Empty);
    }

    internal BranchWithInfo With(
        IOperation? branchValue,
        ControlFlowConditionKind controlFlowConditionKind)
    {
        return new BranchWithInfo(Destination, EnteringRegions, LeavingRegions,
            FinallyRegions, Kind, branchValue, controlFlowConditionKind,
            LeavingRegionLocals, LeavingRegionFlowCaptures);
    }

    private static IEnumerable<ControlFlowRegion> GetTransitiveNestedRegions(ControlFlowRegion region)
    {
        yield return region;

        foreach (var nestedRegion in region.NestedRegions)
        {
            foreach (var transitiveNestedRegion in GetTransitiveNestedRegions(nestedRegion))
            {
                yield return transitiveNestedRegion;
            }
        }
    }

    private static IEnumerable<ILocalSymbol> ComputeLeavingRegionLocals(ImmutableArray<ControlFlowRegion> leavingRegions)
    {
        return leavingRegions.SelectMany(s_getTransitiveNestedRegions).Distinct().SelectMany(r => r.Locals);
    }

    private static IEnumerable<CaptureId> ComputeLeavingRegionFlowCaptures(ImmutableArray<ControlFlowRegion> leavingRegions)
    {
        return leavingRegions.SelectMany(s_getTransitiveNestedRegions).Distinct().SelectMany(r => r.CaptureIds);
    }

    private static ControlFlowConditionKind GetControlFlowConditionKind(ControlFlowBranch branch)
    {
        if (branch.IsConditionalSuccessor ||
            branch.Source.ConditionKind == ControlFlowConditionKind.None)
        {
            return branch.Source.ConditionKind;
        }

        return branch.Source.ConditionKind.Negate();
    }
}

internal static class ControlFlowConditionKindExtensions
{
    public static ControlFlowConditionKind Negate(this ControlFlowConditionKind controlFlowConditionKind)
    {
        switch (controlFlowConditionKind)
        {
            case ControlFlowConditionKind.WhenFalse:
                return ControlFlowConditionKind.WhenTrue;

            case ControlFlowConditionKind.WhenTrue:
                return ControlFlowConditionKind.WhenFalse;

            default:
                Debug.Fail($"Unsupported conditional kind: '{controlFlowConditionKind}'");
                return controlFlowConditionKind;
        }
    }
}
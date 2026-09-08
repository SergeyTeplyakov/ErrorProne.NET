using System.Collections.Generic;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.DisposableAnalyzers;

internal sealed class DisposeAnalysisHelper
{
    private readonly List<INamedTypeSymbol> _disposableExceptions;

    public INamedTypeSymbol? IDisposable { get; }
    public INamedTypeSymbol? IAsyncDisposable { get; }
    public INamedTypeSymbol? IConfigureAsyncDisposable { get; }

    public DisposeAnalysisHelper(Compilation compilation)
    {
        _disposableExceptions = CreateExceptions(compilation);

        IDisposable = compilation.GetTypeByFullName(WellKnownTypeNames.SystemIDisposable);
        IAsyncDisposable = compilation.GetTypeByFullName(WellKnownTypeNames.SystemIAsyncDisposable);
        IConfigureAsyncDisposable = compilation.GetTypeByFullName("System.Runtime.CompilerServices.ConfiguredAsyncDisposable");
    }

    private static List<INamedTypeSymbol> CreateExceptions(Compilation compilation)
    {
        var result = new List<INamedTypeSymbol>();

        // Preserve the prototype's low-noise defaults; these are policy exemptions.
        addIfNotNull(compilation.TaskType());
        addIfNotNull(compilation.TaskOfTType());
        addIfNotNull(compilation.GetTypeByFullName("System.IO.StringReader"));
        addIfNotNull(compilation.GetTypeByFullName("System.IO.MemoryStream"));

        return result;

        void addIfNotNull(INamedTypeSymbol? type)
        {
            if (type is not null)
            {
                result.Add(type);
            }
        }
    }

    public bool IsDisposableTypeNotRequiringToBeDisposed(ITypeSymbol? typeSymbol)
    {
        if (typeSymbol is null)
        {
            return false;
        }

        return _disposableExceptions.Any(e => typeSymbol.DerivesFrom(e));
    }

    public bool IsDisposable(ITypeSymbol? typeSymbol)
    {
        if (typeSymbol is null)
        {
            return false;
        }

        return typeSymbol.IsDisposable(IDisposable, IAsyncDisposable, IConfigureAsyncDisposable);
    }

    public bool ShouldBeDisposed(ITypeSymbol? typeSymbol)
    {
        if (typeSymbol is null)
        {
            return false;
        }

        return IsDisposable(typeSymbol) && !IsDisposableTypeNotRequiringToBeDisposed(typeSymbol);
    }
}
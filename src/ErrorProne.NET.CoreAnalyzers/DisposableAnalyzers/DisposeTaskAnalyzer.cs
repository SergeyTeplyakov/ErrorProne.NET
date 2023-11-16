using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErrorProne.NET.Core;

namespace ErrorProne.NET.DisposableAnalyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DisposeTaskAnalyzer : DiagnosticAnalyzerBase
{
    /// <inheritdoc />
    public override bool ReportDiagnosticsOnGeneratedCode => false;

    /// <nodoc />
    public DisposeTaskAnalyzer()
        : base(DiagnosticDescriptors.ERP042, DiagnosticDescriptors.ERP043)
    {
    }

    /// <inheritdoc />
    protected override void InitializeCore(AnalysisContext context)
    {
        context.RegisterOperationAction(AnalyzeUsing, OperationKind.Using);
        context.RegisterOperationAction(AnalyzeUsingDeclaration, OperationKind.UsingDeclaration);
    }

    private void AnalyzeUsingDeclaration(OperationAnalysisContext context)
    {
        var usingDeclaration = (IUsingDeclarationOperation)context.Operation;

        // looking for a variable initializer inside 'using var xxx = ...'
        var variableInitializer = usingDeclaration.EnumerateChildren().OfType<IVariableInitializerOperation>().FirstOrDefault();

        if (variableInitializer == null)
        {
            return;
        }

        if (variableInitializer.Value.Type is INamedTypeSymbol t && t.IsTaskLike(context.Compilation))
        {
            if (t.TypeArguments.Length == 0)
            {

            }
        }
        



    }

    private void AnalyzeUsing(OperationAnalysisContext context)
    {
        
    }

}

public static class OperationsExtensions
{
    public static IEnumerable<IOperation> EnumerateParents(this IOperation? operation)
    {
        while (operation != null)
        {
            operation = operation.Parent;
            yield return operation;
        }
    }

    public static IEnumerable<IOperation> EnumerateChildren(this IOperation? operation)
    {
        if (operation is null)
        {
            yield break;
        }

        yield return operation;
        foreach (var o in operation.Children.SelectMany(c => c.EnumerateChildren()))
        {
            yield return o;
        }
    }
}

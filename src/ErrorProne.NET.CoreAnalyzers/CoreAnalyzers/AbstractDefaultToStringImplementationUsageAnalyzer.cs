﻿using System.Diagnostics.CodeAnalysis;
using System.Text;
using ErrorProne.NET.Core;
using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.AsyncAnalyzers
{
    /// <summary>
    /// A base class for analyzing <see cref="object.ToString"/> calls.
    /// </summary>
    public abstract class AbstractDefaultToStringImplementationUsageAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        protected AbstractDefaultToStringImplementationUsageAnalyzer(DiagnosticDescriptor diagnostics)
            : base(diagnostics)
        {
        }

        protected abstract bool TryCreateDiagnostic(Compilation compilation, ITypeSymbol type, Location location, [NotNullWhen(true)]out Diagnostic? diagnostic);

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterOperationAction(AnalyzeConversion, OperationKind.Conversion);
            context.RegisterOperationAction(AnalyzeInterpolation, OperationKind.Interpolation);
            context.RegisterOperationAction(AnalyzeMethodInvocation, OperationKind.Invocation);
        }

        private void AnalyzeMethodInvocation(OperationAnalysisContext context)
        {
            var methodCall = (IInvocationOperation)context.Operation;
            if (methodCall.TargetMethod.Name == nameof(ToString) &&
                methodCall.TargetMethod.ContainingType.SpecialType is SpecialType.System_Object or SpecialType
                    .System_ValueType)
            {
                if (TryCreateDiagnostic(
                    context.Compilation,
                    methodCall.Instance.Type,
                    methodCall.Syntax.GetLocation(),
                    out var diagnostic))
                {
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private void AnalyzeInterpolation(OperationAnalysisContext context)
        {
            // This method checks for $"foobar: {taskLikeThing}";
            if (context.Operation is IInterpolationOperation interpolationOperation)
            {
                if (TryCreateDiagnostic(
                        context.Compilation,
                        interpolationOperation.Expression.Type,
                        interpolationOperation.Expression.Syntax.GetLocation(),
                        out var diagnostic))
                {
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private void AnalyzeConversion(OperationAnalysisContext context)
        {
            // This method checks for "something" + taskLikeThing;
            // or string.Format("{0}", taskLikeThing);
            if (context.Operation is IConversionOperation conversion && conversion.Type?.SpecialType is SpecialType.System_String or SpecialType.System_Object)
            {
                // A dangerous conversion is happening when the type of the type of the parent operation is string,
                // like in "FooBar: " + FooBarAsync()
                // Or the type of the grand parent operation is string,
                // like in string.Format("{0}", FooBarAsync())
                if ((isToStringConversion(conversion.Parent)) && conversion.Operand.Type is not null)
                {
                    if (TryCreateDiagnostic(
                            context.Compilation,
                            conversion.Operand.Type, 
                            context.Operation.Syntax.GetLocation(),
                            out var diagnostic))
                    {
                        context.ReportDiagnostic(diagnostic);
                    }
                }

                // another special case: StringBuilder.Append(object) is called.
                if (conversion.Type.SpecialType == SpecialType.System_Object &&
                    conversion.Parent?.Parent is IInvocationOperation invocation &&
                    invocation.TargetMethod.Name == nameof(StringBuilder.Append) &&
                    invocation.TargetMethod.ContainingType.IsClrType(context.Compilation, typeof(StringBuilder)))
                {
                    if (TryCreateDiagnostic(
                        context.Compilation,
                        conversion.Operand.Type,
                        context.Operation.Syntax.GetLocation(),
                        out var diagnostic))
                    {
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }

            static bool isToStringConversion(IOperation? operation)
            {
                if (operation?.Type?.SpecialType == SpecialType.System_String)
                {
                    return true;
                }

                if (operation?.Parent is IInvocationOperation invocation &&
                    invocation.TargetMethod.Name == nameof(string.Format) &&
                    invocation.TargetMethod.ReceiverType?.SpecialType == SpecialType.System_String)
                {
                    // Special casing 'string.Format'.
                    return true;
                }

                return false;
            }
        }
    }
}
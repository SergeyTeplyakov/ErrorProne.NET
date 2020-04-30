using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.CoreAnalyzers.NonDefaultStructs
{
    /// <summary>
    /// A base class for analyzing structs marked with <seealso cref="DoNotUseDefaultConstructionAttributeName"/>.
    /// </summary>
    public abstract class DefaultStructConstructionAnalyzerBase : DiagnosticAnalyzerBase
    {
        protected const string DoNotUseDefaultConstructionAttributeName = "DoNotUseDefaultConstructionAttribute";

        protected static readonly List<Type> SpecialTypes = new List<Type>
        {
            typeof(ImmutableArray<>)
        };

        protected DefaultStructConstructionAnalyzerBase(DiagnosticDescriptor descriptor,
            params DiagnosticDescriptor[] diagnostics) : base(descriptor, diagnostics)
        {
        }

        protected static void ReportDiagnosticForTypeIfNeeded(
            Compilation compilation,
            SyntaxNode syntax,
            ITypeSymbol? type,
            DiagnosticDescriptor rule,
            Action<Diagnostic> reportDiagnostic)
        {
            if (type == null)
            {
                return;
            }
            
            // DoNotUseDefaultConstruction attribute can take a custom error message in the constructor.
            if (HasDoNotUseDefaultConstructionOrSpecial(compilation, type, out var message))
            {
                message ??= string.Empty;
                
                if (!string.IsNullOrEmpty(message))
                {
                    message = " " + message;
                }
                
                reportDiagnostic(
                    Diagnostic.Create(rule, syntax.GetLocation(), type.Name, message));
            }
        }

        protected static bool HasDoNotUseDefaultConstructionOrSpecial(Compilation compilation, ITypeSymbol type, out string? message)
        {
            message = null;
            
            var attributes = type.GetAttributes();
            var doNotUseDefaultAttribute = attributes.FirstOrDefault(a =>
                a.AttributeClass.Name.StartsWith(DoNotCreateStructWithNoDefaultStructConstructionAttributeAnalyzer.DoNotUseDefaultConstructionAttributeName));
            
            if (doNotUseDefaultAttribute != null)
            {
                var constructorArg = doNotUseDefaultAttribute.ConstructorArguments.FirstOrDefault();
                message = !constructorArg.IsNull ? constructorArg.Value?.ToString() : null;
                return true;
            }

            if (SpecialTypes.Any(t => type.IsClrType(compilation, t)))
            {
                return true;
            }

            return false;
        }
    }
}
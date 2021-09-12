using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ErrorProne.NET.Core;
using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;

namespace ErrorProne.Net.StructAnalyzers.NonDefaultStructs
{
    /// <summary>
    /// A base class for analyzing structs marked with <see cref="NonDefaultableStructAnalysis.NonDefaultableAttributeName"/>.
    /// </summary>
    public abstract class NonDefaultableStructAnalyzerBase : DiagnosticAnalyzerBase
    {
        protected NonDefaultableStructAnalyzerBase(DiagnosticDescriptor descriptor,
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

            // NonDefaultableAttribute attribute can take a custom error message in the constructor.
            if (type.DoNotUseDefaultConstruction(compilation, out var message))
            {
                message ??= string.Empty;
                
                if (!string.IsNullOrEmpty(message))
                {
                    message = " " + message;
                }
                
                reportDiagnostic(Diagnostic.Create(rule, syntax.GetLocation(), type.Name, message));
            }
        }
    }
}
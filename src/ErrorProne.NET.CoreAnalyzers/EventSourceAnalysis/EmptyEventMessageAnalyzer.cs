using System.Diagnostics.Tracing;
using System.Linq;
using ErrorProne.NET.Core;
using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.EventSourceAnalysis
{
    /// <summary>
    /// An analyzer that warns when an <c>[Event(...)]</c> attribute sets <c>Message</c> to an empty string.
    /// </summary>
    /// <remarks>
    /// An empty <c>Message</c> makes the runtime emit an empty ETW manifest string-table entry
    /// (<c>&lt;string value=""/&gt;</c>). On Windows Server 2025 the <c>TdhLoadManifest()</c> API no longer
    /// tolerates such entries, so loading the manifest fails and the entire provider is silently dropped.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class EmptyEventMessageAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public static DiagnosticDescriptor Rule => DiagnosticDescriptors.ERP043;

        /// <nodoc />
        public EmptyEventMessageAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
        }

        private void AnalyzeMethod(SymbolAnalysisContext context)
        {
            var method = (IMethodSymbol)context.Symbol;
            if (!method.ContainingType.BaseType.EnumerateBaseTypesAndSelf()
                .Any(type => type.IsClrType(context.Compilation, typeof(EventSource))))
            {
                return;
            }

            var eventAttribute = method.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.IsClrType(context.Compilation, typeof(EventAttribute)) == true);
            if (eventAttribute is null)
            {
                return;
            }

            var messageArg = eventAttribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Message");

            // Only trigger on a compile-time constant empty string. A missing 'Message', a single space,
            // or any non-empty message is fine.
            if (messageArg.Key != "Message" ||
                messageArg.Value.Value is not string message ||
                message.Length != 0)
            {
                return;
            }

            // Prefer reporting on the 'Message = ""' argument syntax so the fixer can target just that argument.
            var attributeSyntax = eventAttribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) as AttributeSyntax;
            var messageArgSyntax = attributeSyntax?.ArgumentList?.Arguments
                .FirstOrDefault(arg => arg.NameEquals?.Name.Identifier.ValueText == "Message");

            var location = messageArgSyntax?.GetLocation()
                ?? method.TryGetDeclarationSyntax()?.Identifier.GetLocation();
            if (location is null)
            {
                return;
            }

            var error =
                $"Event {method.Name} has an empty Message. An empty Message emits an empty ETW manifest " +
                "string-table entry that fails to load on Windows Server 2025 and drops the entire provider. " +
                "Remove the Message argument or set it to a single space.";
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, method.ContainingType.Name, error));
        }
    }
}

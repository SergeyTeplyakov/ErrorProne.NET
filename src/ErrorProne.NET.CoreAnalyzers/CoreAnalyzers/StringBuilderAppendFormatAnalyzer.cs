using System;
using System.Text;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ErrorProne.NET.CoreAnalyzers
{
    /// <summary>
    /// An analyzer that warns on incorrect or suspicious usages of <code>StringBuilder.AppendFormat</code>.
    /// </summary>
    /// <remarks>
    /// This analyzer detects the following potentially erroneous usages of <code>StringBuilder.AppendFormat</code>:
    /// * `AppendFormat` takes an interpolated string that "captures" (and calls `ToString()`) on custom objects
    ///   that can ended up in runtime failure because the resulting string would not ba a valid format string.
    /// * `AppendFormat` takes a string (interpolated or not) without providing extra arguments.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class StringBuilderAppendFormatAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc /> // used by tests
        public static readonly DiagnosticDescriptor DiagnosticDescriptor = 
            new DiagnosticDescriptor(id: DiagnosticId, title: Title, messageFormat: MessageFormat,
                category: Category, defaultSeverity: Severity, description: Description, isEnabledByDefault: true);

        /// <nodoc />
        public const string DiagnosticId = DiagnosticIds.SuspiciousStringBuilderAppendFormatUsage;

        private const string Title = "Suspicious usage of StringBuilder.AppendFormat method.";

        private const string MessageFormat = "{0}";

        private const string Description = "Suspicious usage of StringBuilder.AppendFormat invocation that can cause runtime errors.";

        private const string Category = "CodeSmell";

        private const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <nodoc />
        public StringBuilderAppendFormatAnalyzer()
            : base(supportFading: true, DiagnosticDescriptor)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        }

        private void AnalyzeInvocation(OperationAnalysisContext context)
        {
            var invocation = (IInvocationOperation) context.Operation;
            var target = invocation.TargetMethod;
            if (target.ReceiverType.IsClrType(context.Compilation, typeof(StringBuilder))
                && target.Name == nameof(StringBuilder.AppendFormat))
            {
                CheckRedundantAppendFormatInvocation(context, invocation);
                
                if (invocation.Arguments.Length > 0)
                {
                    var o = invocation.Arguments[0].Value;

                    if (o is IInterpolatedStringOperation interpolatedStringOperation)
                    {
                        foreach (var part in interpolatedStringOperation.Parts)
                        {
                            if (part is IInterpolationOperation io)
                            {
                                var expressionType = io.Expression?.Type;
                                if (expressionType != null)
                                {
                                    Console.WriteLine(expressionType);
                                }
                            }
                            Console.WriteLine(part);
                        }
                    }
                    var i = o.GetType();
                    Console.WriteLine(i);
                }
            }
        }

        private void CheckRedundantAppendFormatInvocation(OperationAnalysisContext context, IInvocationOperation invocation)
        {
            if (invocation.Arguments.Length == 1)
            {
                // This is sb.AppendFormat("Text") kind of invocation.
                string error = "Unnecessary AppendFormat call. Use 'Append' instead.";
                context.ReportDiagnostic(
                    Diagnostic.Create(Descriptor, invocation.Syntax.GetLocation(), error));
            }

        }
    }
}
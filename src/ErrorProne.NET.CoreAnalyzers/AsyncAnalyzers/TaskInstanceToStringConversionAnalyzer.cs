using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using ErrorProne.NET.Core;

namespace ErrorProne.NET.AsyncAnalyzers
{
    /// <summary>
    /// The analyzer warns when a task instance is implicitly converted to string.
    /// </summary>
    /// <remarks>
    /// Here is an example: <code>var r = FooAsync(); Console.WriteLine($"r: {r}");</code>
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class TaskInstanceToStringConversionAnalyzer : AbstractDefaultToStringImplementationUsageAnalyzer
    {
        /// <nodoc />
        public static DiagnosticDescriptor Rule => DiagnosticDescriptors.EPC18;

        /// <nodoc />
        public TaskInstanceToStringConversionAnalyzer()
            : base(Rule)
        {
        }

        protected override bool TryCreateDiagnostic(TaskTypesInfo info, ITypeSymbol type, Location location, [NotNullWhen(true)]out Diagnostic? diagnostic)
        {
            diagnostic = null;

            if (type.IsTaskLike(info))
            {
                diagnostic = Diagnostic.Create(Rule, location);
            }

            return diagnostic != null;
        }
    }
}
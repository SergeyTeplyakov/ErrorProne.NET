using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.AsyncAnalyzers
{
    /// <summary>
    /// The analyzer warns when a task instance is implicitly converted to string.
    /// </summary>
    /// <remarks>
    /// Here is an example: <code>var r = FooAsync(); Console.WriteLine($"r: {r}");</code>
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DefaultToStringImplementationUsageAnalyzer : AbstractDefaultToStringImplementationUsageAnalyzer
    {
        /// <nodoc />
        public static DiagnosticDescriptor Rule => DiagnosticDescriptors.EPC20;

        /// <inheritdoc />
        public override bool ReportDiagnosticsOnGeneratedCode { get; } = true;

        /// <nodoc />
        public DefaultToStringImplementationUsageAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override bool TryCreateDiagnostic(TaskTypesInfo info, ITypeSymbol type, Location location, [NotNullWhen(true)]out Diagnostic? diagnostic)
        {
            diagnostic = null;

            if (type.SpecialType == SpecialType.System_Object)
            {
                // Its fine to call 'ToString' or do the conversion from 'object'.
                return false;
            }

            if (type.IsAbstract)
            {
                // We can't emit a warning for abstract types or interfaces, because we don't know if the derive type would have a custom ToString impl or not.
                return false;
            }

            if (type.TypeKind == TypeKind.TypeParameter)
            {
                // Can't assume anything about the generic types as well.
                return false;
            }

            if (NoToStringOverride(type, out var typeWithNoToString))
            {
                diagnostic = Diagnostic.Create(Rule, location, typeWithNoToString);
            }

            return diagnostic != null;
        }

        private static bool NoToStringOverride(ITypeSymbol type, [NotNullWhen(true)]out ITypeSymbol? typeWithNoToString)
        {
            typeWithNoToString = null;

            if (type.IsTupleType && type is INamedTypeSymbol namedType)
            {
                // Getting the first type from the tuple with no ToString impl.
                var noToString = namedType.TupleElements.Select(te => te.Type).Where(t => !t.OverridesToString());
                typeWithNoToString = noToString.FirstOrDefault();
            }
            else
            {
                if (!type.OverridesToString())
                {
                    typeWithNoToString = type;
                }
            }

            return typeWithNoToString != null;
        }
    }
}
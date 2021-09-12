using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.StructAnalyzers
{
    /// <summary>
    /// Diagnostics produced by this project.
    /// </summary>
    public static class DiagnosticIds
    {
        /// <nodoc />
        public const string MakeStructReadonlyDiagnosticId = "EPS01";

        /// <nodoc />
        public const string UseInModifierForReadOnlyStructDiagnosticId = "EPS05";

        /// <nodoc />
        public const string HiddenStructCopyDiagnosticId = "EPS06";
        
        /// <nodoc />
        public const string HashTableIncompatibilityDiagnosticId = "EPS07";
        
        /// <nodoc />
        public const string DefaultEqualsOrHashCodeIsUsedInStructDiagnosticId = "EPS08";

        /// <nodoc />
        public const string ExplicitInParameterDiagnosticId = "EPS09";

        /// <nodoc />
        public const string DoNotUseDefaultConstructionForStruct = "EPS10";

        /// <nodoc />
        public const string DoNotEmbedStructsMarkedWithDoUseDefaultConstructionForStruct = "EPS11";

        /// <nodoc />
        public const string MakeStructMemberReadOnly = "EPS12";
    }

    public static class Diagnostics
    {
        public const string UsageCategory = "Usage";

        public static DiagnosticDescriptor EPS13 { get; } = new DiagnosticDescriptor(
            nameof(EPS13),
            "A non-defaultable struct must declare a constructor.",
            "A non-defaultable struct {0} must declare a constructor.",
            UsageCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            description: "A non-defaultable struct must be created with a constructor so you must declare one in order to use it.");
    }
}
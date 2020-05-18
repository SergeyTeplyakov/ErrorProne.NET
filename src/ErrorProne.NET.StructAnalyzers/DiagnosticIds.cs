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
}
namespace ErrorProne.NET.Core.CoreAnalyzers
{
    /// <summary>
    /// Diagnostic ids produced by analyzers defined in ErrorProne.Net.Core project.
    /// </summary>
    public static class DiagnosticIds
    {
        /// <nodoc />
        public const string SuspiciousEqualsMethodImplementation = "EPC11";

        /// <nodoc />
        public const string SuspiciousExceptionHandling = "EPC12";
        
        /// <nodoc />
        public const string UnobservedResult = "EPC13";
    }
}
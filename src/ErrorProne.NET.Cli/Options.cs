namespace ErrorProne.NET.Cli
{
    /// <summary>
    /// Set of options that could be provided via command line.
    /// </summary>
    public sealed class Options
    {
        public string? Solution { get; set; }

        public string? LogFile { get; set; }

        public bool RunInfoLevelDiagnostics { get; set; }
        
        public string[]? DisabledDiagnostics { get; set; }

        public string? Analyzer { get; set; }
    }
}
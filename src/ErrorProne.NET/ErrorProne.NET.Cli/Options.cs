using CommandLine;

namespace ErrorProne.NET.Cli
{
    /// <summary>
    /// Set of options that could be provided via command line.
    /// </summary>
    public sealed class Options
    {
        [Option('s', "solution", Required = true, HelpText = "Path to solution to analyzer")]
        public string Solution { get; set; }

        [Option('l', "log", Required = false, HelpText = "Log file where diagnostic information would be stored")]
        public string LogFile { get; set; }

        [Option('i', "info", Required = false, HelpText = "Enable information-level diagnostics", DefaultValue = true)]
        public bool RunInfoLevelDiagnostics { get; set; }
        
        [OptionArray('d', "disable", Required = false, HelpText = "List of diagnostics that should be excluded from the analysis")]
        public string[] DisabledDiagnostics { get; set; }

        [Option('a', "analyzer", Required = false, HelpText = "Path to an analyzer to run. By default all ErrorProne*.dll will be used.")]
        public string Analyzer { get; set; }
    }
}
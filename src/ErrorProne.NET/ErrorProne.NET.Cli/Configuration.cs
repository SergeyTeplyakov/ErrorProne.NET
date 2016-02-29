using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.IO;
using System.Reflection;

namespace ErrorProne.NET.Cli
{
    /// <summary>
    /// Configuration that can be read from the file or built using <see cref="Options"/>.
    /// </summary>
    public sealed class Configuration
    {
        public Configuration(Options options, Assembly analyzer)
        {
            Contract.Requires(options != null);
            Contract.Requires(options.Solution != null);
            Contract.Requires(File.Exists(options.Solution));
            Contract.Requires(!string.IsNullOrEmpty(options.LogFile));
            Contract.Requires(analyzer != null);

            Solution = options.Solution;
            LogFile = options.LogFile;
            RunInfoLevelDiagnostics = options.RunInfoLevelDiagnostics;
            DisabledDiagnostics = (options.DisabledDiagnostics ?? new string[] {}).ToImmutableHashSet();
            Analyzer = analyzer;
        }

        public string Solution { get; }

        public string LogFile { get; }

        public bool LogEnabled => !string.IsNullOrEmpty(LogFile);

        public bool RunInfoLevelDiagnostics { get; }

        public ImmutableHashSet<string> DisabledDiagnostics { get; }

        public Assembly Analyzer { get; }
    }
}
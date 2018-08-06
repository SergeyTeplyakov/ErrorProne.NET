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
        public Configuration(Options options, ImmutableList<Assembly> analyzers)
        {
            Contract.Requires(options != null);
            Contract.Requires(options.Solution != null);
            Contract.Requires(File.Exists(options.Solution));
            Contract.Requires(!string.IsNullOrEmpty(options.LogFile));
            Contract.Requires(analyzers != null && analyzers.Count != 0);

            Solution = options.Solution;
            LogFile = options.LogFile;
            RunInfoLevelDiagnostics = options.RunInfoLevelDiagnostics;
            SuppressedDiagnostics = (options.DisabledDiagnostics ?? new string[] {}).ToImmutableHashSet();
            Analyzers = analyzers;
        }

        public string Solution { get; }

        public string LogFile { get; }

        public bool LogEnabled => !string.IsNullOrEmpty(LogFile);

        public bool RunInfoLevelDiagnostics { get; }

        public ImmutableHashSet<string> SuppressedDiagnostics { get; }

        public ImmutableList<Assembly> Analyzers { get; }
    }
}
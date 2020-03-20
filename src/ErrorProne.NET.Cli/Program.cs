using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using ErrorProne.NET.Cli.Utilities;
using Microsoft.Extensions.CommandLineUtils;
using static ErrorProne.NET.Cli.CustomLogger;

namespace ErrorProne.NET.Cli
{
    /// <summary>
    /// Simple application that allows to run specified analyser for specifierd solution.
    /// </summary>
    internal static class Program
    {
        private static Options? ParseCommandLineArgs(string[] args)
        {
            var app = new CommandLineApplication(throwOnUnexpectedArg: false);

            var options = new Options();
            
            var solution = app.Option(
                "-s | --solution <solution>",
                "Required. Path to a solution to analyze.",
                CommandOptionType.SingleValue);
            var analyzer = app.Option(
                "-a | --analyzer <analyzer>",
                "Optional. Path to an analyzer to run. Default is all the ErrorProne*.dll analyzers.",
                CommandOptionType.SingleValue);
            var info = app.Option(
                "-i | --info",
                "Optional. Enable information-level diagnostics. Default is 'true'.",
                CommandOptionType.NoValue);
            var logFile = app.Option(
                "-l | --log <logFile>",
                "Optional. Path to a log file. log.txt is the default.",
                CommandOptionType.SingleValue);
            var disabledDiagnostics = app.Option(
                "-d | --disabled <diagnostics>",
                "Optional. List of diagnostics to exclude",
                CommandOptionType.MultipleValue);
            app.ExtendedHelpText =
                "Example: ErrorProne.NET.Cli.exe -s:my.sln -l:foo.txt -d:ERP01 -d:ERP02";

            app.HelpOption("-? | -h | --help");

            app.OnExecute(() =>
            {
                if (!solution.HasValue())
                {
                    return 1;
                }

                options.Solution = solution.Value();
                if (analyzer.HasValue())
                {
                    options.Analyzer = analyzer.Value();
                }

                if (logFile.HasValue())
                {
                    options.LogFile = logFile.Value();
                }

                if (info.HasValue())
                {
                    options.RunInfoLevelDiagnostics = true;
                }

                if (disabledDiagnostics.HasValue())
                {
                    options.DisabledDiagnostics = disabledDiagnostics.Values.ToArray();
                }

                return 0;
            });

            int result = app.Execute(args);
            if (result == 1)
            {
                app.ShowHelp();
                return null;
            }

            return options;
        }

        private static Configuration? ValidateOptions(Options options)
        {
            if (Path.GetExtension(options.Solution) != ".sln")
            {
                WriteError($"'{options.Solution}' is not a valid solution file.");
                return null;
            }

            if (!File.Exists(options.Solution))
            {
                WriteInfo($"Provided solution file ('{options.Solution}') does not exists. ");
                return null;
            }

            if (!string.IsNullOrEmpty(options.LogFile))
            {
                options.LogFile = Path.GetFullPath(options.LogFile);
                WriteInfo($"Log file enabled ('{options.LogFile}')");

                FileUtilities.TryDeleteIfNeeded(options.LogFile);
            }

            if (options.DisabledDiagnostics != null && options.DisabledDiagnostics.Length != 0)
            {
                WriteInfo($"Disabled diagnostics: {string.Join(", ", options.DisabledDiagnostics)}");
            }

            try
            {
                var executable = Assembly.GetExecutingAssembly().Location;
                var executableDirectory = Path.GetDirectoryName(executable);
                Console.WriteLine($"Executing assembly: {executableDirectory}");

                var analyzerPaths = Directory.EnumerateFiles(executableDirectory, "ErrorProne.NET*.dll");
                var analyzers = analyzerPaths.Select(a => Assembly.LoadFile(a)).ToImmutableList();

                if (analyzers.IsEmpty)
                {
                    WriteError($"Can't find any ErrorProne.NET analyzers at '{executable}'.");
                    return null;
                }

                return new Configuration(options, analyzers);
            }
            catch (Exception e)
            {
                WriteError($"Failed to load ErrorProne.NET analyzers.\r\n{e}");
                return null;
            }
        }

        private static void Main(string[] args)
        {
            var options = ParseCommandLineArgs(args);
            if (options == null)
            {
                return;
            }

            var configuration = ValidateOptions(options);
            if (configuration == null)
            {
                return;
            }

            Configure(configuration.LogFile, configuration.LogEnabled);

            LogConfiguration(configuration);

            var runner = new AnalyzerRunner(configuration);
            runner.TryAnalyzeSolutionAsync().GetAwaiter().GetResult();

            Console.WriteLine("Press \"Enter\" to exit");
            Console.ReadLine();
        }

        private static void LogConfiguration(Configuration configuration)
        {
            CustomLogger.WriteInfo($"Analyzing solution '{configuration.Solution}'.");
            WriteInfo($"Logging is {(configuration.LogEnabled ? "enabled" : "disabled")}", consoleOnly: true);
            if (configuration.LogEnabled)
            {
                WriteInfo($"Log file is '{configuration.LogFile}'", consoleOnly: true);
            }

            if (!configuration.Analyzers.IsEmpty)
            {
                WriteInfo($"Custom analyzers: {string.Join(", ", configuration.Analyzers.Select(a => a.FullName))}");
            }

            WriteInfo($"Info level diagnostics: {(configuration.RunInfoLevelDiagnostics ? "'on'" : "'off'")}");
            if (!configuration.SuppressedDiagnostics.IsEmpty)
            {
                WriteInfo($"Suppressed diagnostics: {string.Join(", ", configuration.SuppressedDiagnostics)}");
            }
        }
    }
}

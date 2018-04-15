using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using CommandLine.Text;
using ErrorProne.NET.Cli.Utilities;
using static ErrorProne.NET.Cli.CustomLogger;

namespace ErrorProne.NET.Cli
{
    /// <summary>
    /// Simple application that allows to run specified analyser for specifierd solution.
    /// </summary>
    internal static class Program
    {
        const string AnalyzerAssemblyName = "ErrorProne.NET*.dll";

        private static Options ParseCommandLineArgs(string[] args)
        {
            var options = new Options();
            bool result = CommandLine.Parser.Default.ParseArguments(args, options);
            if (!result)
            {
                Console.WriteLine(HelpText.AutoBuild(options).ToString());
                return null;
            }

            return options;
        }

        private static Configuration ValidateOptions(Options options)
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
                var executablePath = Assembly.GetExecutingAssembly().Location;
                Console.WriteLine($"Executing assembly: " + Path.GetDirectoryName(executablePath));
                var analyzerPaths = Directory.EnumerateFiles(Path.GetDirectoryName(executablePath), "ErrorProne.NET*.dll");
                var analyzers = analyzerPaths.Select(a => Assembly.LoadFile(a)).ToImmutableList();

                return new Configuration(options, analyzers);
            }
            catch (Exception e)
            {
                WriteError($"Failed to load ErrorProne analyzers\r\n{e}");
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

            CustomLogger.Configure(configuration.LogFile, configuration.LogEnabled);

            var runner = new AnalyzerRunner(configuration);
            runner.TryAnalyzeSolutionAsync().GetAwaiter().GetResult();

            Console.WriteLine("Press \"Enter\" to exit");
            Console.ReadLine();
        }
    }
}

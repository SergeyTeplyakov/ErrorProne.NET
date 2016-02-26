using System;
using System.IO;
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
        const string AnalyzerAssemblyName = "ErrorProne.NET.dll";

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

            try
            {
                var executablePath = Assembly.GetExecutingAssembly().Location;
                var analyzerFullPath = Path.Combine(Path.GetDirectoryName(executablePath), AnalyzerAssemblyName);
                var analyzer = Assembly.LoadFile(analyzerFullPath);
                return new Configuration(options, analyzer);
            }
            catch (Exception e)
            {
                WriteError($"Failed to load ErrorProne.NET.dll\r\n{e}");
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

            CustomLogger.Configure(configuration.LogFile, true);

            var runner = new AnalyzerRunner(configuration);
            runner.TryAnalyzeSolutionAsync().GetAwaiter().GetResult();
            
            Console.WriteLine("Press \"Enter\" to exit");
            Console.ReadLine();
        }
    }
}

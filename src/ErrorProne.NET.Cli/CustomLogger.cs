using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.ContractsLight;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.Cli
{
    /// <summary>
    /// Very simple (and even naive) logger.
    /// </summary>
    internal static class CustomLogger
    {
        private static volatile bool FileLoggerEnabled;
        private static volatile string? LogFileName;

        public static void Configure(string logFileName, bool fileLoggerEnabled)
        {
            Contract.Requires(logFileName != null || fileLoggerEnabled);

            LogFileName = logFileName;
            FileLoggerEnabled = fileLoggerEnabled;
        }

        public static void WriteLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        public static void WriteInfo(string text, bool consoleOnly = false)
        {
            WriteLine(text, ConsoleColor.White);
            if (!consoleOnly)
            {
                WriteFile(text + Environment.NewLine);
            }
        }

        public static void WriteCaption(string text)
        {
            WriteLine(text, ConsoleColor.Green);
        }

        public static void WriteWarning(string text)
        {
            WriteLine(text, ConsoleColor.DarkYellow);
        }

        public static void WriteError(string text)
        {
            WriteLine(text, ConsoleColor.DarkRed);
        }

        private static readonly Dictionary<DiagnosticSeverity, Action<string>> ConsoleDiagnosticPrinters = new Dictionary<DiagnosticSeverity, Action<string>>()
        {
            [DiagnosticSeverity.Hidden] = (message) => { },
            [DiagnosticSeverity.Error] = (message) => WriteError(message),
            [DiagnosticSeverity.Warning] = (message) => WriteWarning(message),
            [DiagnosticSeverity.Info] = (message) => WriteInfo(message),
        };

        private static readonly object ConsoleLock = new object();
        // Need to lock around writing to the file to avoid sharing violation
        private static readonly object FileLockEnabled = new object();

        public static void PrintStatistics(List<ProjectAnalysisResult> diagnostics, ImmutableArray<DiagnosticAnalyzer> analyzers)
        {
            Contract.Requires(diagnostics != null);

            var analyzerDictionary = analyzers.SelectMany(a => a.SupportedDiagnostics).ToDictionarySafe(a => a.Id, a => a);

            var result =
                diagnostics
                    .SelectMany(d => d.Diagnostics)
                    .GroupBy(d => d.Id)
                    .Select(
                        g =>
                            new
                            {
                                Message = $"Diagnostic {g.Key}: {analyzerDictionary[g.Key].Title} -- {g.Count()} issues",
                                Severity = analyzerDictionary[g.Key].DefaultSeverity
                            })
                    .OrderBy(r => r.Severity);

            foreach (var e in result)
            {
                WriteInfo(e.Message);
                WriteFile(e.Message);
            }
        }

        public static void LogDiagnostics(Project project, ImmutableArray<Diagnostic> diagnostics)
        {
            var caption = CreateCaption($"Found {diagnostics.Length} diagnostic in project '{project.Name}'");

            var orderedDiagnostics = 
                diagnostics
                .OrderBy(i => i.Id)
                .ThenBy(i => i.Location.SourceTree?.FilePath ?? "")
                .ThenBy(i => i.Location.SourceSpan.Start)
                .ToList();

            lock (ConsoleLock)
            {
                WriteCaption(caption);

                foreach (var rd in orderedDiagnostics)
                {
                    ConsoleDiagnosticPrinters[rd.Severity](rd.ToString());
                }
            }

            string logEntry = $"{caption}\r\n{string.Join("\r\n", orderedDiagnostics)}";
            WriteFile(logEntry);
        }

        private static void WriteFile(string message)
        {
            if (FileLoggerEnabled)
            {
                lock (FileLockEnabled)
                {
                    try
                    {
                        File.AppendAllText(LogFileName, message);
                    }
                    catch (Exception e)
                    {
                        WriteError($"Failed to write a log file:{Environment.NewLine}:{e}");
                    }
                }
            }
        }

        private static string CreateCaption(string text)
        {
            return 
                "//------------------------------------------------------------\r\n" +
                $"// {text}\r\n" +
                "//------------------------------------------------------------";
        }
    }
}
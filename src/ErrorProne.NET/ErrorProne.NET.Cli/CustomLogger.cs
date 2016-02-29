using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Cli
{
    /// <summary>
    /// Very simple (and even naive) logger.
    /// </summary>
    internal static class CustomLogger
    {
        private static volatile bool _fileLoggerEnabled;
        private static volatile string _logFileName;

        public static void Configure(string logFileName, bool fileLoggerEnabled)
        {
            _logFileName = logFileName;
            _fileLoggerEnabled = fileLoggerEnabled;
        }

        public static void WriteLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        public static void WriteInfo(string text)
        {
            WriteLine(text, ConsoleColor.White);
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

        private static readonly object _consoleLock = new object();
        // Need to lock around writing to the file to avoid sharing violation
        private static readonly object _fileLockEnabled = new object();

        public static void LogDiagnostics(Project project, ImmutableArray<Diagnostic> diagnostics)
        {
            var caption = CreateCaption($"Found {diagnostics.Length} diagnostic in project '{project.Name}'");

            var orderedDiagnostics = 
                diagnostics
                .OrderBy(i => i.Id)
                .ThenBy(i => i.Location.SourceTree?.FilePath ?? "")
                .ThenBy(i => i.Location.SourceSpan.Start)
                .ToList();

            lock (_consoleLock)
            {
                WriteCaption(caption);

                foreach (var rd in orderedDiagnostics)
                {
                    ConsoleDiagnosticPrinters[rd.Severity](rd.ToString());
                }
            }

            if (_fileLoggerEnabled)
            {
                lock (_fileLockEnabled)
                {
                    string logEntry = $"{caption}\r\n{string.Join("\r\n", orderedDiagnostics)}";
                    try
                    {
                        File.AppendAllText(_logFileName, logEntry);
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
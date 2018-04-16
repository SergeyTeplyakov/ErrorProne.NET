using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ErrorProne.NET.Cli.Extensions;
using ErrorProne.NET.Cli.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;

using static ErrorProne.NET.Cli.CustomLogger;

namespace ErrorProne.NET.Cli
{
    internal class ProjectAnalysisResult
    {
        public ProjectAnalysisResult(Project project, ImmutableArray<Diagnostic> diagnostics)
        {
            Contract.Requires(project != null);
            Project = project;

            Diagnostics = diagnostics;
        }

        public Project Project { get; }

        public ImmutableArray<Diagnostic> Diagnostics { get; }
    }

    public sealed class AnalyzerRunner
    {
        private readonly Configuration _configuration;

        public AnalyzerRunner(Configuration configuration)
        {
            Contract.Requires(configuration != null);
            _configuration = configuration;
        }

        public async Task TryAnalyzeSolutionAsync()
        {
            try
            {
                await AnalyzeSolutionAsync(_configuration);
            }
            catch (Exception e)
            {
                WriteError($"Failed to analyze solution: {Environment.NewLine}{e}");
            }
        }

        private async Task<List<ProjectAnalysisResult>> AnalyseSolutionAsync(Solution solution, ImmutableArray<DiagnosticAnalyzer> analyzers, Configuration configuration)
        {
            var ruleIds = analyzers
                .SelectMany(a => a.SupportedDiagnostics.Select(d => d.Id))
                .Where(d => !_configuration.DisabledDiagnostics.Contains(d))
                .ToImmutableHashSet();

            var projectAnalysisTasks = solution.Projects
                // First, Running analysis
                .Select(p => new { Project = p, Task = AnalyzeProjectAsync(p, analyzers) })
                .ToList()
                // Then we need to print all the results
                .Select(p => new { Project = p.Project, Task = p.Task.ContinueWith(t =>
                    {
                        var diagnostics = t.Result.Where(d => ruleIds.Contains(d.Id)).ToImmutableArray();
                        if (!configuration.RunInfoLevelDiagnostics)
                        {
                            diagnostics = diagnostics.Where(d => d.Severity != DiagnosticSeverity.Info).ToImmutableArray();
                        }
                        LogDiagnostics(p.Project, diagnostics);

                        return t;
                    }).Unwrap()})
                .ToList();

            // And need to wait when all the analysis and printing tasks would be finished
            await Task.WhenAll(projectAnalysisTasks.Select(p => p.Task));

            // And only after now we can build the result
            var result =
                projectAnalysisTasks
                .Select(r => new ProjectAnalysisResult(r.Project, r.Task.Result.Where(d => ruleIds.Contains(d.Id)).ToImmutableArray())).ToList();

            return result;
        }

        private static async Task<ImmutableArray<Diagnostic>> AnalyzeProjectAsync(Project project, ImmutableArray<DiagnosticAnalyzer> analyzers)
        {
            WriteInfo($"Running analysis for '{project.Name}'...");

            var compilation = await project.GetCompilationAsync();
            var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
            var result = await compilationWithAnalyzers.GetAllDiagnosticsAsync();

            return result;
        }

        private async Task AnalyzeSolutionAsync(Configuration configuration)
        {
            var analyzers = GetAnalyzers(configuration.Analyzers).ToImmutableArray();

            // Loading the solution
            var sw = Stopwatch.StartNew();

            var workspace = MSBuildWorkspace.Create();

            WriteInfo($"Opening solution '{configuration.Solution}'...");

            var solution = await workspace.OpenSolutionAsync(configuration.Solution);

            WriteInfo($"Loaded solution in {sw.ElapsedMilliseconds}ms with '{solution.ProjectIds.Count}' projects and '{solution.DocumentsCount()}' documents");

            WriteInfo("Running the analysis...");

            // Running the analysis
            sw.Restart();

            var diagnostics = await AnalyseSolutionAsync(solution, analyzers, configuration);

            WriteInfo($"Found {diagnostics.SelectMany(d => d.Diagnostics).Count()} diagnostics in {sw.ElapsedMilliseconds}ms");

            PrintStatistics(diagnostics, analyzers);
        }

        private static List<DiagnosticAnalyzer> GetAnalyzers(Assembly assembly)
        {
            var diagnosticAnalyzerType = typeof(DiagnosticAnalyzer);

            return
                assembly
                    .GetTypes()
                    .Where(type => type.IsSubclassOf(diagnosticAnalyzerType) && !type.IsAbstract)
                    .Select(CustomActivator.CreateInstance<DiagnosticAnalyzer>)
                    .ToList();
        }

        private static List<DiagnosticAnalyzer> GetAnalyzers(IEnumerable<Assembly> assemblies)
        {
            return assemblies.SelectMany(a => GetAnalyzers(a)).ToList();
        }
    }
}
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace RoslynNUnitTestRunner
{
    public static partial class CSharpCodeFixVerifier<TAnalyzer, TCodeFix>
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TCodeFix : CodeFixProvider, new()
    {
        public class Test : CSharpCodeFixTest<TAnalyzer, TCodeFix, NUnitVerifier>
        {
            public Test()
            {
                SolutionTransforms.Add((solution, projectId) =>
                {
                    var project = solution.GetProject(projectId);
                    var parseOptions = (CSharpParseOptions)project.ParseOptions;
                    solution = solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion));

                    return solution;
                });

                TestState.AdditionalReferences.Add(AdditionalMetadataReferences.SystemCollections);
                TestState.AdditionalReferences.Add(AdditionalMetadataReferences.SystemCollectionsConcurrent);
                TestState.AdditionalReferences.Add(AdditionalMetadataReferences.SystemConsole);
                TestState.AdditionalReferences.Add(AdditionalMetadataReferences.SystemRuntime);
            }

            public LanguageVersion LanguageVersion { get; set; } = LanguageVersion.CSharp7_3;
        }
    }
}

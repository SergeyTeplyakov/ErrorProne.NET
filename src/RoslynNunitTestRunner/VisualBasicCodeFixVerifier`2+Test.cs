using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Testing;

namespace RoslynNUnitTestRunner
{
    public static partial class VisualBasicCodeFixVerifier<TAnalyzer, TCodeFix>
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TCodeFix : CodeFixProvider, new()
    {
        public class Test : VisualBasicCodeFixTest<TAnalyzer, TCodeFix, NUnitVerifier>
        {
            public Test()
            {
                SolutionTransforms.Add((solution, projectId) =>
                {
                    var project = solution.GetProject(projectId);
                    var parseOptions = (VisualBasicParseOptions)project.ParseOptions;
                    solution = solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion));

                    return solution;
                });

                TestState.AdditionalReferences.Add(AdditionalMetadataReferences.SystemCollections);
                TestState.AdditionalReferences.Add(AdditionalMetadataReferences.SystemCollectionsConcurrent);
                TestState.AdditionalReferences.Add(AdditionalMetadataReferences.SystemConsole);
                TestState.AdditionalReferences.Add(AdditionalMetadataReferences.SystemRuntime);
            }

            public LanguageVersion LanguageVersion { get; set; } = LanguageVersion.VisualBasic15_5;
        }
    }
}

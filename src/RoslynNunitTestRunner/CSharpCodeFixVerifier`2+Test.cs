using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace ErrorProne.NET.TestHelpers
{
    public static partial class CSharpCodeFixVerifier<TAnalyzer, TCodeFix>
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TCodeFix : CodeFixProvider, new()
    {
        public static Task VerifyAsync(string code)
        {
            return new Test
            {
                TestState = { Sources = { code } },

            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        public class Test : CSharpCodeFixTest<TAnalyzer, TCodeFix, NUnitVerifier>
        {
            public Test()
            {
                ReferenceAssemblies = AdditionalMetadataReferences.ReferenceAssemblies;

                SolutionTransforms.Add((solution, projectId) =>
                {
                    var project = solution.GetProject(projectId);
                    var parseOptions = (CSharpParseOptions?)project?.ParseOptions;
                    if (parseOptions == null)
                    {
                        return solution;
                    }

                    solution = solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion));

                    return solution;
                });
            }

            public LanguageVersion LanguageVersion { get; set; } = LanguageVersion.CSharp8;
        }
    }
}

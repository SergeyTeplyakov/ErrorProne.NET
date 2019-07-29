using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynNUnitTestRunner;

namespace ErrorProne.NET.CoreAnalyzers.Tests.Allocations
{

    static class AllocationTestHelper
    {
        public static void VerifyCode<TAnalyzer>(string code, bool injectAssemblyLevelConfigurationAttribute = true) where TAnalyzer : DiagnosticAnalyzer, new()
        {
            // enable all the allocation analyzers by adding an assembly level attribute
            VerifyCodeAsync<TAnalyzer>(code, injectAssemblyLevelConfigurationAttribute).GetAwaiter().GetResult();
        }

        public static Task VerifyCodeAsync<TAnalyzer>(string code, bool injectAssemblyLevelConfigurationAttribute = true) where TAnalyzer : DiagnosticAnalyzer, new()
        {
            // enable all the allocation analyzers by adding an assembly level attribute
            return VerifyCodeImpl<TAnalyzer>(code, injectAssemblyLevelConfigurationAttribute);
        }

        private static Task VerifyCodeImpl<TAnalyzer>(string code, bool injectAssemblyLevelConfigurationAttribute = false) where TAnalyzer : DiagnosticAnalyzer, new()
        {
            var test = new CSharpCodeFixVerifier<TAnalyzer, EmptyCodeFixProvider>.Test
            {
                TestState =
                {
                    Sources =
                    {
                        code,
                    },
                },
            }.WithoutGeneratedCodeVerification().WithHiddenAllocationsAttributeDeclaration();

            if (injectAssemblyLevelConfigurationAttribute)
            {
                test = test.WithAssemblyLevelHiddenAllocationsAttribute();
            }

            return test.RunAsync();
        }
    }

    struct Struct
    {
        public void Method()
        {
        }

        public static void StaticMethod()
        {
        }
    }

    struct StructWithOverrides
    {
        public override string ToString() => string.Empty;
        public override int GetHashCode() => 42;
        public override bool Equals(object other) => true;
    }

    struct ComparableStruct : IComparable
    {
        public int CompareTo(object obj) => 0;
    }

    [Flags]
    enum E
    {
        V1 = 1,
        V2 = 1 << 1
    }
}

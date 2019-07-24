using System;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynNUnitTestRunner;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    static class AllocationTestHelper
    {
        public static void VerifyCode<TAnalyzer>(string code) where TAnalyzer : DiagnosticAnalyzer, new() =>
            new CSharpCodeFixVerifier<TAnalyzer, EmptyCodeFixProvider>.Test
            {
                TestState =
                {
                    Sources =
                    {
                        code
                    },
                },
            }.WithoutGeneratedCodeVerification().RunAsync().GetAwaiter().GetResult();
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

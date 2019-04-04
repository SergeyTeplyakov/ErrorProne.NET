﻿using ErrorProne.NET.AsyncAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.RemoveConfigureAwaitAnalyzer,
    ErrorProne.NET.AsyncAnalyzers.RemoveConfigureAwaitCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class RemoveConfigureAwaitCodeFixProviderTests
    {
        [Test]
        public async Task RemoveConfigureAwaitFalse()
        {
            string code = @"
[assembly:DoNotUseConfigureAwait()]
public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42).ConfigureAwait(false);
    }
}";

            string expected = @"
[assembly:DoNotUseConfigureAwait()]
public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42);
    }
}";

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        DiagnosticResult.CompilerError("CS0246").WithSpan(2, 11, 2, 33).WithMessage("The type or namespace name 'DoNotUseConfigureAwaitAttribute' could not be found (are you missing a using directive or an assembly reference?)"),
                        DiagnosticResult.CompilerError("CS0246").WithSpan(2, 11, 2, 33).WithMessage("The type or namespace name 'DoNotUseConfigureAwait' could not be found (are you missing a using directive or an assembly reference?)"),
                        VerifyCS.Diagnostic(RemoveConfigureAwaitAnalyzer.Rule).WithSeverity(DiagnosticSeverity.Hidden).WithSpan(7, 52, 7, 73).WithMessage("bar"),
                    },
                },
                FixedState =
                {
                    Sources = { expected },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task RemoveConfigureAwaitFalse_With_Right_Formatting()
        {
            string code = @"
[assembly:DoNotUseConfigureAwait()]
public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42)
         .ConfigureAwait(false);
    }
}";

            string expected = @"
[assembly:DoNotUseConfigureAwait()]
public class MyClass
{
    public static async System.Threading.Tasks.Task Foo()
    {
       await System.Threading.Tasks.Task.Delay(42);
    }
}";

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        DiagnosticResult.CompilerError("CS0246").WithSpan(2, 11, 2, 33).WithMessage("The type or namespace name 'DoNotUseConfigureAwaitAttribute' could not be found (are you missing a using directive or an assembly reference?)"),
                        DiagnosticResult.CompilerError("CS0246").WithSpan(2, 11, 2, 33).WithMessage("The type or namespace name 'DoNotUseConfigureAwait' could not be found (are you missing a using directive or an assembly reference?)"),
                        VerifyCS.Diagnostic(RemoveConfigureAwaitAnalyzer.Rule).WithSeverity(DiagnosticSeverity.Hidden).WithSpan(8, 11, 8, 32).WithMessage("bar"),
                    },
                },
                FixedState =
                {
                    Sources = { expected },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
    }
}
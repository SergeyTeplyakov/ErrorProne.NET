using Microsoft.CodeAnalysis;
using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.SuspiciousExceptionHandlingAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.SuspiciousExeptionHandling
{
    [TestFixture]
    public class OnlyMessageIsUsedTests
    {
        [Test]
        public async Task Warn_When_Only_Message_Is_Used()
        {
            string code = @"
class FooBar {
  public static void Foo() {
    try { new object(); }
    catch(System.Exception e) {System.Console.WriteLine(e.Message);}
  }
}
";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(SuspiciousExceptionHandlingAnalyzer.DiagnosticDescriptor).WithSpan(5, 57, 5, 58).WithArguments("e"),
                        VerifyCS.Diagnostic(SuspiciousExceptionHandlingAnalyzer.DiagnosticDescriptor).WithSeverity(DiagnosticSeverity.Hidden).WithSpan(5, 59, 5, 66).WithMessage("bar"),
                    },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task Warn_When_Only_Message_Is_Used_For_AggregateException()
        {
            string code = @"
class FooBar {
  public static void Foo() {
    try { new object(); }
    catch(System.AggregateException e) {System.Console.WriteLine(e.Message);}
  }
}
";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(SuspiciousExceptionHandlingAnalyzer.DiagnosticDescriptor).WithSpan(5, 66, 5, 67).WithArguments("e"),
                        VerifyCS.Diagnostic(SuspiciousExceptionHandlingAnalyzer.DiagnosticDescriptor).WithSeverity(DiagnosticSeverity.Hidden).WithSpan(5, 68, 5, 75).WithMessage("bar"),
                    },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task Warn_When_Only_Message_Is_Used_For_TargetInvocationException()
        {
            string code = @"
class FooBar {
  public static void Foo() {
    try { new object(); }
    catch(System.Reflection.TargetInvocationException e) {System.Console.WriteLine(e.Message);}
  }
}
";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(SuspiciousExceptionHandlingAnalyzer.DiagnosticDescriptor).WithSpan(5, 84, 5, 85).WithArguments("e"),
                        VerifyCS.Diagnostic(SuspiciousExceptionHandlingAnalyzer.DiagnosticDescriptor).WithSeverity(DiagnosticSeverity.Hidden).WithSpan(5, 86, 5, 93).WithMessage("bar"),
                    },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task Warn_When_Only_Message_Is_Used_For_TypeLoadException()
        {
            string code = @"
class FooBar {
  public static void Foo() {
    try { new object(); }
    catch(System.TypeLoadException e) {System.Console.WriteLine(e.Message);}
  }
}
";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(SuspiciousExceptionHandlingAnalyzer.DiagnosticDescriptor).WithSpan(5, 65, 5, 66).WithArguments("e"),
                        VerifyCS.Diagnostic(SuspiciousExceptionHandlingAnalyzer.DiagnosticDescriptor).WithSeverity(DiagnosticSeverity.Hidden).WithSpan(5, 67, 5, 74).WithMessage("bar"),
                    },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task NoWarn_When_Only_Message_Is_Used_But_Exception_Is_Not_Generic()
        {
            string code = @"
class FooBar {
  public static void Foo() {
    try { new object(); }
    catch(System.ArgumentException e) {System.Console.WriteLine(e.Message);}
  }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }
    }
}
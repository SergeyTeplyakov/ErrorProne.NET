using ErrorProne.NET.TestHelpers;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.SuspiciousExceptionHandlingAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.SuspiciousExceptionHandling
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
                        VerifyCS.Diagnostic(SuspiciousExceptionHandlingAnalyzer.Rule).WithSpan(5, 59, 5, 66).WithArguments("e"),
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
                        VerifyCS.Diagnostic(SuspiciousExceptionHandlingAnalyzer.Rule).WithSpan(5, 68, 5, 75).WithArguments("e"),
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
                        VerifyCS.Diagnostic(SuspiciousExceptionHandlingAnalyzer.Rule).WithSpan(5, 86, 5, 93).WithArguments("e"),
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
                        VerifyCS.Diagnostic(SuspiciousExceptionHandlingAnalyzer.Rule).WithSpan(5, 67, 5, 74).WithArguments("e"),
                    },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
        [Test]
        public async Task Warn_On_Anonymous_Usage_Of_ExceptionMessage()
        {
            string code = @"
using System.Collections;
using System;
class Test
{
    public ArrayList LoadList(string key, string subKey = """") {
      var errors=new ArrayList();
      try { new object();
      } catch (Exception exception) {
        errors.Add($""{new { key, subKey, exception.Message }}"");
      }
    return errors;
    }
}";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(SuspiciousExceptionHandlingAnalyzer.Rule).WithSpan(10, 52, 10, 59).WithArguments("exception"),
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
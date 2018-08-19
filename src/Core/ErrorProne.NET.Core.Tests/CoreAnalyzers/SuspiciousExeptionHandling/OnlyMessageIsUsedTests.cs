using System;
using ErrorProne.NET.Core.CoreAnalyzers;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Core.Tests.CoreAnalyzersTests.SuspiciousExeptionHandling
{
    [TestFixture]
    public class OnlyMessageIsUsedTests : CSharpAnalyzerTestFixture<SuspiciousExceptionHandlingAnalyzer>
    {
        public const string DiagnosticId = SuspiciousExceptionHandlingAnalyzer.DiagnosticId;

        [Test]
        public void Warn_When_Only_Message_Is_Used()
        {
            string code = @"
class FooBar {
  public static void Foo() {
    try { new object(); }
    catch(System.Exception e) {System.Console.WriteLine([|e|].[|Message|]);}
  }
}
";
            HasDiagnostics(code, new[] { DiagnosticId, DiagnosticId + "WithoutSuggestion" });
        }

        [Test]
        public void Warn_When_Only_Message_Is_Used_For_AggregateException()
        {
            string code = @"
class FooBar {
  public static void Foo() {
    try { new object(); }
    catch(System.AggregateException e) {System.Console.WriteLine([|e|].[|Message|]);}
  }
}
";
            HasDiagnostics(code, new []{DiagnosticId, DiagnosticId + "WithoutSuggestion" });
        }

        [Test]
        public void Warn_When_Only_Message_Is_Used_For_TargetInvocationException()
        {
            string code = @"
class FooBar {
  public static void Foo() {
    try { new object(); }
    catch(System.Reflection.TargetInvocationException e) {System.Console.WriteLine([|e|].[|Message|]);}
  }
}
";
            HasDiagnostics(code, new []{DiagnosticId, DiagnosticId + "WithoutSuggestion" });
        }

        [Test]
        public void Warn_When_Only_Message_Is_Used_For_TypeLoadException()
        {
            string code = @"
class FooBar {
  public static void Foo() {
    try { new object(); }
    catch(System.TypeLoadException e) {System.Console.WriteLine([|e|].[|Message|]);}
  }
}
";
            HasDiagnostics(code, new []{DiagnosticId, DiagnosticId + "WithoutSuggestion" });
        }

        [Test]
        public void NoWarn_When_Only_Message_Is_Used_But_Exception_Is_Not_Generic()
        {
            string code = @"
class FooBar {
  public static void Foo() {
    try { new object(); }
    catch(System.ArgumentException e) {System.Console.WriteLine(e.Message);}
  }
}
";
            NoDiagnostic(code, DiagnosticId);
        }
    }
}
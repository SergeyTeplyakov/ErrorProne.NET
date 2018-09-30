using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.CoreAnalyzers.Tests.CoreAnalyzers.SuspiciousExeptionHandling
{
    [TestFixture]
    public class RemoveExMessageCodeFixProviderTests : CSharpCodeFixTestFixture<ExceptionHandlingFixers>
    {
        [Test]
        public void RemoveExMessage()
        {
            string code = @"
class FooBar {
  public static void Foo() {
    try { new object(); }
    catch(System.Exception e) {System.Console.WriteLine(e.[|Message|]);}
  }
}";

            string expected = @"
class FooBar {
  public static void Foo() {
    try { new object(); }
    catch(System.Exception e) {System.Console.WriteLine(e);}
  }
}";

            TestCodeFix(code, expected, SuspiciousExceptionHandlingAnalyzer.DiagnosticDescriptor);
        }
    }
}
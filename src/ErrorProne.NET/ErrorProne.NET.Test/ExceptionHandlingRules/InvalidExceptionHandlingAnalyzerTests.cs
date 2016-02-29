using ErrorProne.NET.Common;
using ErrorProne.NET.Rules.ExceptionHandling;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.ExceptionHandlingRules
{
    [TestFixture]
    public class InvalidExceptionHandlingAnalyzerTests : CSharpAnalyzerTestFixture<ExceptionHandlingAnalyer>
    {
        [Test]
        public void WarnOnWritingLineMessage()
        {
            string code = @"
class Test
{
  public void Foo()
  {
    try { new object(); }
    catch(System.Exception ex) {System.Console.WriteLine([|ex.Message|]);}
  }
}";
            HasDiagnostic(code, RuleIds.OnlyExceptionMessageWasObserved);
        }

        [Test]
        public void WarnOnWritingLineMessageWithIf()
        {
            string code = @"
class Test
{
  public void Foo()
  {
    try { new object(); }
    catch(System.Exception ex)
    {
        if (ex != null)
        {
           System.Console.WriteLine([|ex.Message|]);
        }
        System.Console.WriteLine([|ex.Message|]);
    }
  }
}";
            HasDiagnostic(code, RuleIds.OnlyExceptionMessageWasObserved);
        }
        
        [Test]
        public void NoWarningIfExceptionWasConditionallyObserved()
        {
            string code = @"
class Test
{
  public void Foo()
  {
    try { new object(); }
    catch(System.Exception ex)
    {
        if (ex != null)
        {
           System.Console.WriteLine(ex);
        }
        System.Console.WriteLine(ex.Message);
    }
  }
}";

            NoDiagnostic(code, RuleIds.OnlyExceptionMessageWasObserved);
        }

        [Test]
        public void NoWarningOnOtherExceptionTypes()
        {
            string code = @"
class Test
{
  public void Foo()
  {
    try { new object(); }
    catch(System.ArgumentException ex)
    {
        System.Console.WriteLine(ex.Message);
    }
  }
}";
            NoDiagnostic(code, RuleIds.OnlyExceptionMessageWasObserved);
        }

        [Test]
        public void NoWarningIfExceptionWasStored()
        {
            string code = @"
class Test
{
  private System.Exception _field;
  public void Foo()
  {
    try { new object(); }
    catch(System.Exception ex)
    {
        _field = ex;
        System.Console.WriteLine(ex.Message);
    }
  }
}";
            NoDiagnostic(code, RuleIds.OnlyExceptionMessageWasObserved);
        }
    }
}
using System;
using ErrorProne.NET.Common;
using ErrorProne.NET.Rules.ExceptionHandling;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.ExceptionHandlingRules
{
    [TestFixture]
    public class ThrowExAnalyzerTests : CSharpAnalyzerTestFixture<ThrowExAnalyzer>
    {
        [Test]
        public void NoWarningWhenThrowingInstanceVariable()
        {
            var test = @"
class Test
{
  private readonly Exception ex;
  public void Foo()
  {
      try { Console.WriteLine(); }
      catch(Exception ex) {throw this.ex;}
  }
}";

            NoDiagnostic(test, RuleIds.IncorrectExceptionPropagation);
        }

        [Test]
        public void NoWarningOnEmptyCatch()
        {
            var test = @"
class Test
{
  private readonly Exception ex;
  public void Foo()
  {
     try { Console.WriteLine(); }
     catch {throw ex;}
  }
}";

            NoDiagnostic(test, RuleIds.IncorrectExceptionPropagation);
        }

        [Test]
        public void WarningOnThrowWithEnclosingFieldEx()
        {
            var test = @"
class Test
{
  private readonly Exception ex;
  public void Foo()
  {
     try { Console.WriteLine(); }
     catch(System.Exception ex) {throw [|ex|];}
  }
}";

            HasDiagnostic(test, RuleIds.IncorrectExceptionPropagation);
        }

        [Test]
        public void WarningOnThrowEx()
        {
            var test = @"
class Test
{
  public void Foo()
  {
     try { Console.WriteLine(); }
     catch(System.Exception ex) {throw [|ex|];}
  }
}";

            HasDiagnostic(test, RuleIds.IncorrectExceptionPropagation);
        }

        [Test]
        public void WarningOnThrowExComplex()
        {
            var test = @"
using System;
class Test
{
    public void Foo(int arg)
    {
        Exception ex0 = new Exception();
        try { Console.WriteLine(); }
        catch(Exception ex)
        {
            Console.WriteLine(ex0);
            Exception ex2 = null;
            Console.WriteLine(ex2);
                    
            if (arg == 42)
                throw [|ex|];
            else
                throw [|ex|];
        }
    }
}";

            HasDiagnostic(test, RuleIds.IncorrectExceptionPropagation);
        }


        [Test]
        public void TestTwoWarnings()
        {
            var test = @"
class Test
{
    public void Foo()
    {
        try { System.Console.WriteLine(); }
        catch(System.Exception ex)
        {
            if (ex.Message.Length == 5) throw [|ex|];
            throw [|ex|];
        }
    }
}";

            HasDiagnostic(test, RuleIds.IncorrectExceptionPropagation);
        }
    }
}
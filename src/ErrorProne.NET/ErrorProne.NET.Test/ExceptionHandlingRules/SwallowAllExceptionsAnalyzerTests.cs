using ErrorProne.NET.Common;
using ErrorProne.NET.ExceptionHandlingRules;
using NUnit.Framework;

namespace RoslynNunitTestRunner.Reflection
{
    [TestFixture]
    public class SwallowAllExceptionsAnalyzerTests : CSharpAnalyzerTestFixture<SwallowAllExceptionAnalyzer>
    {
        [Test]
        public void WarnOnEmptyCatchBlock()
        {
            string code = @"
class Test
{
  public void Foo()
  {
    try { new object(); }
    catch {[|}|]
  }
}";
            HasDiagnostic(code, RuleIds.AllExceptionSwalled);
        }

        [Test]
        public void WarnOnCatchWithStatementBlock()
        {
            string code = @"
using System;
class Test
{
  public void Foo()
  {
    try { new object(); }
    catch {Console.WriteLine(42);[|}|]
  }
}";
            HasDiagnostic(code, RuleIds.AllExceptionSwalled);
        }

        [Test]
        public void WarnOnException()
        {
            string code = @"
using System;
class Test
{
  public void Foo()
  {
    try { new object(); }
    catch(Exception) {Console.WriteLine(42);[|}|]
  }
}";
            HasDiagnostic(code, RuleIds.AllExceptionSwalled);
        }

        [Test]
        public void WarningOnEmptyCatchBlockWithConditionalReturn()
        {
            string code = @"
using System;
class Test
{
  public void Foo()
  {
    try { Console.WriteLine(); }
    catch {Console.WriteLine(); if (n == 42) [|return;|] throw;}
  }
}";
            HasDiagnostic(code, RuleIds.AllExceptionSwalled);
        }

        [Test]
        public void WarningOnConditionalObservation()
        {
            string code = @"
using System;
class Test
{
  public void Foo()
  {
    try { Console.WriteLine(); }
    catch(Exception e) {if (e is System.ArggregateException) throw;[|}|]
  }
}";
            HasDiagnostic(code, RuleIds.AllExceptionSwalled);
        }

        [Test]
        public void WarnsOnCatchWithExceptionThatWasNotUsed()
        {
            string code = @"
using System;
class Test
{
  public void Foo(int n)
  {
    try { new object(); }
    catch(Exception e) {if (n != 0) throw; Console.WriteLine(42);[|}|]
  }
}";
            HasDiagnostic(code, RuleIds.AllExceptionSwalled);
        }

        [Test]
        public void NoWarnOnReThrow()
        {
            string code = @"
using System;
class Test
{
  public void Foo()
  {
    try { Console.WriteLine(); }
    catch {Console.WriteLine(); throw;}
  }
}";
            NoDiagnostic(code, RuleIds.AllExceptionSwalled);
        }

        [Test]
        public void NoWarnIfExceptionWasObserved()
        {
            string code = @"
using System;
class Test
{
  public void Foo()
  {
    try { Console.WriteLine(); }
    catch(Exception e) {Console.WriteLine(e.Message); } // should be another warning when only e.Message was observed!
  }
}";
            NoDiagnostic(code, RuleIds.AllExceptionSwalled);
        }


        [Test]
        public void NoWarnsOnNonSystemException()
        {
            string code = @"
using System;
class Test
{
  public void Foo()
  {
    try { new object(); }
    catch(ArgumentException) {Console.WriteLine(42);}
  }
}";
            NoDiagnostic(code, RuleIds.AllExceptionSwalled);
        }
    }
}
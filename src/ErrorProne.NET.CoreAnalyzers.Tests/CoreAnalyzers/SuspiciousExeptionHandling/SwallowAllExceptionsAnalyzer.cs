// --------------------------------------------------------------------
//  
// Copyright (c) Microsoft Corporation.  All rights reserved.
//  
// --------------------------------------------------------------------

using ErrorProne.NET.ExceptionsAnalyzers;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.CoreAnalyzers.Tests.SuspiciousExeptionHandling
{
    [TestFixture]
    public class SwallowAllExceptionsAnalyzerTests : CSharpAnalyzerTestFixture<SwallowAllExceptionsAnalyzer>
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
            HasDiagnostic(code, SwallowAllExceptionsAnalyzer.DiagnosticId);
        }

        [Test]
        public void NoWarnOnCatchWithFilter()
        {
            string code = @"
class Test
{
  public void Foo()
  {
    try { new object(); }
    catch(System.Exception e) when (e is System.IO.IOException) { }
  }
}";
            NoDiagnostic(code, SwallowAllExceptionsAnalyzer.DiagnosticId);
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
            HasDiagnostic(code, SwallowAllExceptionsAnalyzer.DiagnosticId);
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
            HasDiagnostic(code, SwallowAllExceptionsAnalyzer.DiagnosticId);
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
            HasDiagnostic(code, SwallowAllExceptionsAnalyzer.DiagnosticId);
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
            HasDiagnostic(code, SwallowAllExceptionsAnalyzer.DiagnosticId);
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
            HasDiagnostic(code, SwallowAllExceptionsAnalyzer.DiagnosticId);
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
            NoDiagnostic(code, SwallowAllExceptionsAnalyzer.DiagnosticId);
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
            NoDiagnostic(code, SwallowAllExceptionsAnalyzer.DiagnosticId);
        }

        [Test]
        public void NoWarnWhenExceptionObservedInInterpolatedString()
        {
            string code = @"
using System;
class Test
{
  public void Foo()
  {
    try { Console.WriteLine(); }
    catch(Exception e) {Console.WriteLine($""Error: {e}""); } // should be another warning when only e.Message was observed!
  }
}";
            NoDiagnostic(code, SwallowAllExceptionsAnalyzer.DiagnosticId);
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
            NoDiagnostic(code, SwallowAllExceptionsAnalyzer.DiagnosticId);
        }
    }
}
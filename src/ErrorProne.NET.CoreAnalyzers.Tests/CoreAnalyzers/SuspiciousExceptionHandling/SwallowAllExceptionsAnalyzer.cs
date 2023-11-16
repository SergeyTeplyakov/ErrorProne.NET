// --------------------------------------------------------------------
//  
// Copyright (c) Microsoft Corporation.  All rights reserved.
//  
// --------------------------------------------------------------------

using ErrorProne.NET.TestHelpers;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.ExceptionsAnalyzers.SwallowAllExceptionsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.SuspiciousExceptionHandling
{
    [TestFixture]
    public class SwallowAllExceptionsAnalyzerTests
    {
        [Test]
        public async Task WarnOnEmptyCatchBlock()
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
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }




        [Test]
        public async Task DoNotWarnOnAnonymousUsageOfException()
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
        errors.Add($""{new { key, subKey, exception }}"");
      }
    return errors;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task NoWarnOnCatchWithFilter()
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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }
        
        [Test]
        public async Task NoWarnWhenExceptionIsReturned()
        {
            string code = @"
class Test
{
  public System.Exception Foo()
  {
    try { new object(); }
    catch(System.Exception e) { return e;}
    return null;
  }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }
        
        [Test]
        public async Task WarnOnConditionalReturn()
        {
            string code = @"
class Test
{
  public System.Exception Foo()
  {
    try { new object(); }
    catch(System.Exception e) { if (e is System.ArgumentException) return e;[|}|]
    return null;
  }
}";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task WarnOnCatchWithStatementBlock()
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
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task WarnOnException()
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
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task WarningOnEmptyCatchBlockWithConditionalReturn()
        {
            string code = @"
using System;
class Test
{
  public void Foo(int n)
  {
    try { Console.WriteLine(); }
    catch {Console.WriteLine(); if (n == 42) [|return;|] throw;}
  }
}";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task WarningOnConditionalObservation()
        {
            string code = @"
using System;
class Test
{
  public void Foo()
  {
    try { Console.WriteLine(); }
    catch(Exception e) {if (e is System.AggregateException) throw;[|}|]
  }
}";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task WarnsOnCatchWithExceptionThatWasNotUsed()
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
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task NoWarnOnReThrow()
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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task NoWarnIfExceptionWasObserved()
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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }
        
        [Test]
        public async Task NoWarnIfExceptionWasObservedWithStringConcat()
        {
            string code = @"
using System;
class Test
{
  private readonly Action<string> m_logger;
  public void Foo()
  {
    try { Console.WriteLine(); }
    catch(Exception e) {m_logger($""Observed: "" + e.ToString()); }
  }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }
        
        [Test]
        public async Task NoWarnIfExceptionWasObservedInLocalFunction()
        {
            string code = @"
using System;
class Test
{
  public void Foo()
  {
    try { Console.WriteLine(); }
    catch(Exception e) {
      observe();
      void observe() => Console.WriteLine(e);
    }
  }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }
        
        [Test]
        public async Task NoWarnIfExceptionWasObservedByCallingMethod()
        {
            string code = @"
using System;
class Test
{
  public void Foo()
  {
    try { Console.WriteLine(); }
    catch(Exception e) {
      Observe(e);
    }
  }
  static void Observe(Exception e) => Console.WriteLine(e);
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task NoWarnWhenExceptionObservedInInterpolatedString()
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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task NoWarnsOnNonSystemException()
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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }
    }
}
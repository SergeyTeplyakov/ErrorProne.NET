// --------------------------------------------------------------------
//  
// Copyright (c) Microsoft Corporation.  All rights reserved.
//  
// --------------------------------------------------------------------

using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.ExceptionsAnalyzers.ThrowExAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.SuspiciousExeptionHandling
{
    [TestFixture]
    public class ThrowExAnalyzerTests
    {
        [Test]
        public async Task NoWarningWhenThrowingInstanceVariable()
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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task NoWarningOnEmptyCatch()
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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task WarningOnThrowWithEnclosingFieldEx()
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

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { test },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task WarningOnThrowEx()
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

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { test },
                },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task WarningOnThrowExComplex()
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

            await new VerifyCS.Test
            {
                TestState = { Sources = { test } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }


        [Test]
        public async Task TestTwoWarnings()
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

            await new VerifyCS.Test
            {
                TestState = { Sources = { test } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }
    }
}
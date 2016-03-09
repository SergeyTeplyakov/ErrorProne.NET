using ErrorProne.NET.Common;
using ErrorProne.NET.Rules.ExceptionHandling;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.ExceptionHandlingRules
{
    [TestFixture]
    public class NonSynchronousPreconditionsInAsyncMethodTests : CSharpAnalyzerTestFixture<AsyncMethodPreconditionsAnalyzer>
    {
        [Test]
        public void WarnOnSingleIfThrow()
        {
            string code = @"
using System.Threading.Tasks;
class Test
{
  public async Task Foo(string s)
  {
    if (s == null) [|throw new System.ArgumentException();|]
    await Task.Delay(42);
  }
}";
            HasDiagnostic(code, RuleIds.SuspiciousPreconditionInAsyncMethod);
        }

        [Test]
        public void WarnOnFirstIfThrow()
        {
            string code = @"
using System.Threading.Tasks;
class Test
{
  public async Task Foo(string s)
  {
    if (s == null) [|throw new System.ArgumentNullException();|]
    if (s.Length == 1) throw new System.Exception();
    await Task.Delay(42);
  }
}";
            HasDiagnostic(code, RuleIds.SuspiciousPreconditionInAsyncMethod);
        }

        [Test]
        public void WarnOnBothArgumentExcpetions()
        {
            string code = @"
using System.Threading.Tasks;
class Test
{
  public async Task Foo(string s)
  {
    if (s == null) [|throw new System.ArgumentNullException();|]
    if (s.Length == 1) [|throw new System.ArgumentException();|]
    await Task.Delay(42);
  }
}";
            HasDiagnostic(code, RuleIds.SuspiciousPreconditionInAsyncMethod);
        }

        [Test]
        public void WarnOnFirstOnlyBecauseNextStatementIsNotIfThrow()
        {
            string code = @"
using System.Threading.Tasks;
class Test
{
  public async Task Foo(string s)
  {
    if (s == null) [|throw new System.ArgumentNullException();|]
    System.Console.WriteLine(s);
    if (s.Length == 1) throw new System.ArgumentException();
    await Task.Delay(42);
  }
}";
            HasDiagnostic(code, RuleIds.SuspiciousPreconditionInAsyncMethod);
        }

        [Test]
        public void DoNotWarnIfMethodIsNotAsync()
        {
            string code = @"
using System.Threading.Tasks;
class Test
{
  public Task Foo(string s)
  {
    if (s == null) throw new System.ArgumentNullException();
    System.Console.WriteLine(s);
    if (s.Length == 1) throw new System.ArgumentException();

    return Task.FromResult(42);
  }
}";
            NoDiagnostic(code, RuleIds.SuspiciousPreconditionInAsyncMethod);
        }
    }
}
using ErrorProne.NET.Common;
using ErrorProne.NET.Rules.ExceptionHandling;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.ExceptionHandlingRules
{
    [TestFixture]
    public class NonSynchronousPreconditionsInIteratorBlockTests : CSharpAnalyzerTestFixture<IteratorBlockPreconditionsAnalyzer>
    {
        [Test]
        public void WarnOnSingleIfThrow()
        {
            string code = @"
using System.Collections.Generic;
class Test
{
  public IEnumerable<string> Foo(string s)
  {
    if (s == null) [|throw new System.ArgumentException();|]
    yield break;
  }
}";
            HasDiagnostic(code, RuleIds.SuspiciousPreconditionInIteratorBlock);
        }

        [Test]
        public void WarnOnFirstIfThrow()
        {
            string code = @"
using System.Collections.Generic;
class Test
{
  public IEnumerable<string> Foo(string s)
  {
    if (s == null) [|throw new System.ArgumentNullException();|]
    if (s.Length == 1) throw new System.Exception();
    yield break;
  }
}";
            HasDiagnostic(code, RuleIds.SuspiciousPreconditionInIteratorBlock);
        }

        [Test]
        public void WarnOnBothArgumentExcpetions()
        {
            string code = @"
using System.Collections.Generic;
class Test
{
  public IEnumerable<string> Foo(string s)
  {
    if (s == null) [|throw new System.ArgumentNullException();|]
    if (s.Length == 1) [|throw new System.ArgumentException();|]
    yield break;
  }
}";
            HasDiagnostic(code, RuleIds.SuspiciousPreconditionInIteratorBlock);
        }

        [Test]
        public void WarnOnFirstOnlyBecauseNextStatementIsNotIfThrow()
        {
            string code = @"
using System.Collections.Generic;
class Test
{
  public IEnumerable<string> Foo(string s)
  {
    if (s == null) [|throw new System.ArgumentNullException();|]
    System.Console.WriteLine(s);
    if (s.Length == 1) throw new System.ArgumentException();
    yield break;
  }
}";
            HasDiagnostic(code, RuleIds.SuspiciousPreconditionInIteratorBlock);
        }

        [Test]
        public void DoNotWarnIfMethodIsNotIteratorBlock()
        {
            string code = @"
using System.Collections.Generic;
class Test
{
  public IEnumerable<string> Foo(string s)
  {
    if (s == null) throw new System.ArgumentNullException();
    System.Console.WriteLine(s);
    if (s.Length == 1) throw new System.ArgumentException();
  }
}";
            NoDiagnostic(code, RuleIds.SuspiciousPreconditionInIteratorBlock);
        }
    }
}
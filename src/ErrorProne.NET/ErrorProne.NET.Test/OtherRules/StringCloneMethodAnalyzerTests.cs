using System.Collections.Generic;
using ErrorProne.NET.Common;
using ErrorProne.NET.Rules.OtherRules;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.OtherRules
{
    [TestFixture]
    public class StringCloneMethodAnalyzerTests : CSharpAnalyzerTestFixture<StringCloneMethodAnalyzer>
    {
        [Test]
        public void ShouldWarnOnStringClone()
        {
            string code = @"
class Foo {
  public static void Bar(string str) {
    var x = [|str.Clone()|];
  }
}";
            HasDiagnostic(code, RuleIds.StringCloneMethodWasUsed);
        }
    }
}
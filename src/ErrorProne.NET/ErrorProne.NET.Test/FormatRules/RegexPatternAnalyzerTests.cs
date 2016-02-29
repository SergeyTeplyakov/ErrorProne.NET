using System.Collections.Generic;
using ErrorProne.NET.Common;
using ErrorProne.NET.Rules.Formatting;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.StringFormatRules
{
    [TestFixture]
    public class InvalidRegexAnalyzerTests : CSharpAnalyzerTestFixture<RegexPatternAnalyzer>
    {
        private static void Test()
        {
            new System.Text.RegularExpressions.Regex("__FORMAT__");
        }

        [TestCaseSource(nameof(Regexes))]
        public void TestCases(string regex)
        {
            string code = @"
class C
{
    private static void Test()
    {
        var x = new System.Text.RegularExpressions.Regex([|""__FORMAT__""|]);
    }
}".Replace("__FORMAT__", regex);

            HasDiagnostic(code, RuleIds.RegexPatternIsInvalid);
        }

        public static IEnumerable<string> Regexes()
        {
            yield return "\\x";
            yield return "(.+";
        }
    }
}
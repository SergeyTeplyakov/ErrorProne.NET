using System.Collections.Generic;
using ErrorProne.NET.Common;
using ErrorProne.NET.FormatRules;
using ErrorProne.NET.SideEffectRules;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.StringFormatRules
{
    [TestFixture]
    public class StringFormatCorrectnessAnalyzerTests : CSharpAnalyzerTestFixture<StringFormatCorrectnessAnalyzer>
    {
        [TestCaseSource(nameof(FormatTestCases))]
        public void TestCases(string formatModifier)
        {
            string code = @"
class C
{
    private static void Test()
    {
        string s = string.Format([|""$$FORMAT$$""|], 42);
    }
}".Replace("$$FORMAT$$", formatModifier);

            HasDiagnostic(code, RuleIds.StringFormatInvalidId);
        }

        public static IEnumerable<string> FormatTestCases()
        {
            yield return "{{0}";
            yield return "{0}{d}";
        }


        [Test]
        public void ShouldWarnOnInvalidFormatAsConst()
        {
            const string code = @"
class C
{
    const string format = ""{{0}, {4}"";
    private static void Test()
    {
        string s = string.Format(null, [|format|], 42, 3);
    }
}";

            HasDiagnostic(code, RuleIds.StringFormatInvalidId);
        }

        [Test]
        public void ShouldWarnOnCustomInvalidFormatString()
        {
            const string code = @"
namespace JetBrains.Annotations
{
    public sealed class StringFormatMethodAttribute : System.Attribute
    {
        public string FormatParameterName { get; private set; }

        public StringFormatMethodAttribute(string formatParameterName)
        {
            this.FormatParameterName = formatParameterName;
        }
    }
}
    public class Test
    {
        [JetBrains.Annotations.StringFormatMethod(""format"")]
        public static void CustomFormat(string format, int arg, params object[] args)
        {
            CustomFormat([|""{{0}, {1}""|], 1);
        }
    }
";

            HasDiagnostic(code, RuleIds.StringFormatInvalidId);
        }
    }
}
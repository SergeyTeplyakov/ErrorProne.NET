using ErrorProne.NET.Common;
using ErrorProne.NET.FormatRules;
using ErrorProne.NET.SideEffectRules;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.StringFormatRules
{
    [TestFixture]
    public class ExcessiveFormatArgumentTests : CSharpAnalyzerTestFixture<StringFormatCorrectnessAnalyzer>
    {
        [Test]
        public void FailOnExcessiveArgument()
        {
            const string code = @"
class C
{
    private static void Test()
    {
        var sb = new System.Text.StringBuilder();
        string s = sb.AppendFormat(""{0}, {1}"", 1, 2, [|3|]).ToString();
    }
}";

            HasDiagnostic(code, RuleIds.StringFormatHasEcessiveArgumentId);
        }

        [Test]
        public void FailWithExcessiveArgumentInTheMiddle()
        {
            const string code = @"
class C
{
    private static void Test()
    {
        var sb = new System.Text.StringBuilder();
        string s = sb.AppendFormat(""{0}, {2} {4}"", 1, [|2|], 3, [|4|], 5).ToString();
    }
}";

            HasDiagnostic(code, RuleIds.StringFormatHasEcessiveArgumentId);
        }
    }
}
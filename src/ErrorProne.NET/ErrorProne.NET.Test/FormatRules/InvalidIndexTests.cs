using ErrorProne.NET.Common;
using ErrorProne.NET.Rules.Formatting;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.StringFormatRules
{
    [TestFixture]
    public class InvalidIndexTests : CSharpAnalyzerTestFixture<StringFormatCorrectnessAnalyzer>
    {
        [Test]
        public void ShouldWarnOnStringBuilderFormat()
        {
            const string code = @"
class C
{
    private static void Test()
    {
        var sb = new System.Text.StringBuilder();
        string s = sb.AppendFormat([|""{0}, {4}""|], 42).ToString();
    }
}";

            HasDiagnostic(code, RuleIds.StringFormatInvalidIndexId);
        }

        [Test]
        public void ShouldWarnOnConsoleWriteLine()
        {
            const string code = @"
class C
{
    private static void Test()
    {
        System.Console.WriteLine([|""{0}, {4}""|], 42);
    }
}";

            HasDiagnostic(code, RuleIds.StringFormatInvalidIndexId);
        }

        [Test]
        public void ShouldWarnOnStringFormat()
        {
            const string code = @"
class C
{
    private static void Test()
    {
        string s = string.Format([|""{0}, {4}""|], 42);
    }
}";

            HasDiagnostic(code, RuleIds.StringFormatInvalidIndexId);
        }

        [Test]
        public void ShouldWarnOnStringWithFormattedOverload()
        {
            const string code = @"
class C
{
    private static void Test()
    {
        string s = string.Format(null, [|""{0}, {4}, {1}""|], 42, 3);
    }
}";

            HasDiagnostic(code, RuleIds.StringFormatInvalidIndexId);
        }

        [Test]
        public void ShouldWarnOnInvalidArrayCreation()
        {
            const string code = @"
class C
{
    private static void Test()
    {
        string s = string.Format(null, [|""{0}, {4}""|], new object[]{42});
    }
}";

            HasDiagnostic(code, RuleIds.StringFormatInvalidIndexId);
        }

        [Test]
        public void ShouldWarnOnInvalidFormatAsConst()
        {
            const string code = @"
class C
{
    const string format = ""{0}, {4}, {1}"";
    private static void Test()
    {
        string s = string.Format(null, [|format|], 42, 3);
    }
}";

            HasDiagnostic(code, RuleIds.StringFormatInvalidIndexId);
        }

        [Test]
        public void ShouldWarnOnInvalidFormatAsStaticReadonlyField()
        {
            const string code = @"
class C
{
    private static readonly string format = ""{0}, {4}, {1}"";
    private static void Test()
    {
        string s = string.Format(null, [|format|], 42, 3);
    }
}";

            HasDiagnostic(code, RuleIds.StringFormatInvalidIndexId);
        }

        [Test]
        public void ShouldNotWarnIfFormatArgumentIsExpressionThatReturnsArrayOrObject()
        {
            const string code = @"
class C
{
    private static object Args() {return new object[]{0, 1};}
    private static object[] Args2() {return new object[]{0, 1};}
    private static void Test()
    {
        string s = string.Format(""{0}, {1}"", Args());
        string s2 = string.Format(""{0}, {1}"", Args2());
    }
}";

            NoDiagnostic(code, RuleIds.StringFormatInvalidIndexId);
        }

        // TODO: need a special test case for named arguments like string.Format(format: "{0}, {2}", arg0: 1);

        // Another test case: static string GetArgs() { return new object[] { 1, 2, 2, 3, 5, 6 }.ToString(); }
        // Console.WriteLine("{0}, {5}", GetArgs()); <-- Should warn (BTW, R# doesnt!)
        //
        // Another one: static string GetArgs() { return new object[] { 1, 2, 2, 3, 5, 6 }.ToString(); }
        // Console.WriteLine("{0}, {5}", GetArgs()); <-- Should NOT warn BTW object could have multiple arguments
        //
        // Console.WriteLine("{0}, {5}", new object[]{1, 2}); <-- Should warn (BTW, R# doesnt!)


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
            CustomFormat([|""{0}, {1}""|], 1);
        }
    }
";

            HasDiagnostic(code, RuleIds.StringFormatInvalidIndexId);
        }

        [Test]
        public void ShouldWarnOnCustomInvalidFormatStringWithConstAttributeValue()
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
        const string Format = ""format"";
        [JetBrains.Annotations.StringFormatMethod(Format)]
        public static void CustomFormat(string format, int arg, params object[] args)
        {
            CustomFormat([|""{0}, {1}""|], 1);
        }
    }
";

            HasDiagnostic(code, RuleIds.StringFormatInvalidIndexId);
        }

        // test cases:
        // 1. for constant string literal
        // 2. for readonly static string
        // sb.AppendFormat + 
        // 3. for console.writeline (do I need some kind of external annotation storage for this?) And how to check existing methods in BCL for this? Codex? search for argument name format! +
        // 4. Case for excessive argument
        // 5. test for custom string format function that has JB attribute
    }
}
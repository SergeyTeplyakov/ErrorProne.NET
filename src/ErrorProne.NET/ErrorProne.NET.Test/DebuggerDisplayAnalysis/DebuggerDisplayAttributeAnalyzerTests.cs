using System.Collections.Generic;
using ErrorProne.NET.Common;
using ErrorProne.NET.Rules.ExceptionHandling;
using ErrorProne.NET.Rules.OtherRules;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.OtherRules
{
    [TestFixture]
    public class DebuggerDisplayAttributeAnalyzerTests : CSharpAnalyzerTestFixture<DebuggerDisplayAttributeAnanlyzer>
    {
        [Test]
        public void ShouldNotWarnOnValidExpression()
        {
            string code =
@"[System.Diagnostics.DebuggerDisplay(""X: {foo(string.Empty)}"")]
public class CustomType
{
    private static int foo(string n)
    {
        return 42;
    }
}";

            NoDiagnostic(code, RuleIds.DebuggerDisplayAttributeInvalidFormat);
        }

        [Test]
        [TestCase("{_member}")]
        [TestCase("{_member, nq}")]
        [TestCase("{s_staticMember, nq}")]
        [TestCase("{instanceFoo(), nq}")]
        [TestCase("{staticFoo(42), nq}")]
        [TestCase("{staticFoo(42) + this.instanceFoo()}")] // expressions are ok
        public void SuccessCases(string attribute)
        {
            const string Template = @"[System.Diagnostics.DebuggerDisplay(""{0}"")]
public class CustomType
{{
    private int _member = 42;
    private int s_staticMember = 42;
    private string instanceFoo() {{return string.Empty;}}
    private static int staticFoo(int n)
    {{
        return 42;
    }}
}}";

            NoDiagnostic(string.Format(Template, attribute), RuleIds.DebuggerDisplayAttributeInvalidFormat);
        }

        [Test]
        [TestCase("X: {foo(42")] // no closing brace
        [TestCase("X: foo(42)}")] // no open brace
        [TestCase("X: {foo2(42)}")] // unknown method
        [TestCase("X: {foo(42)}")] // 42 is not compatible with type string
        [TestCase("X: {this.foo(string.Empty)}")] // foo is static
        [TestCase("X: {foo(string.Empty) - bar()}")] // no - for string and int
        [TestCase("X: {foo(42}")] // expression is not correct
        [TestCase("X: {foo}")] // should be invocation, but was just reference
        [TestCase("X: {fooWithVoid()}")] // should not call void-return method
        [TestCase("X: {foo(42),nqq}")] // unknown format qualifier
        public void FailureCases(string attribute)
        {
            const string Template = @"


// foo
[System.Diagnostics.DebuggerDisplay([|""{0}""|])]
public class CustomType
{{
    private static int foo(string n)
    {{
        return 42;
    }}
    private static string bar(string n = 0)
    {{
        return string.Empty;
    }}

    private static void fooWithVoid()
    {{
        return 42;
    }}
}}";

            HasDiagnostic(string.Format(Template, attribute), RuleIds.DebuggerDisplayAttributeInvalidFormat);
        }
    }
}
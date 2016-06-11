using System.Collections.Generic;
using ErrorProne.NET.Common;
using ErrorProne.NET.Rules.OtherRules;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.OtherRules
{
    [TestFixture]
    public class ReadOnlyAttributeAnalyzerTests : CSharpAnalyzerTestFixture<DoNotUseReadOnlyAttributeAnalyzer>
    {
        [TestCaseSource(nameof(ShouldWarnIfInvalidTestCases))]
        public void ShouldWarnIfInvalid(string code)
        {
            HasDiagnostic(code, RuleIds.ReadonlyAttributeNotOnCustomStructs);
        }

        public static IEnumerable<string> ShouldWarnIfInvalidTestCases()
        {
            // Can't use attribute on primitives
            yield return @"
class Foo
{
    [ErrorProne.NET.Annotations.ReadOnlyAttribute]
    public int [|_m|];
}";

            // Can't use attribute on nullable primitives
            yield return @"
class Foo
{
    [ErrorProne.NET.Annotations.ReadOnlyAttribute]
    public int? [|_m|];
}";

            // Can't use attribute on reference types
            yield return @"
class Foo
{
    [ErrorProne.NET.Annotations.ReadOnlyAttribute]
    public string [|_m|];
}";

            // Can't use attribute on enums
            yield return @"
enum CustomEnum {}
class Foo
{
    [ErrorProne.NET.Annotations.ReadOnlyAttribute]
    public CustomEnum [|_m|];
}";

            // Can't use with readonly attribute
            yield return @"
struct CustomStruct {}
class Foo
{
    [ErrorProne.NET.Annotations.ReadOnlyAttribute]
    public readonly CustomStruct [|_m|];
}";
        }

        [TestCaseSource(nameof(ShouldNotWarnTestCases))]
        public void ShouldNotWarnIfValid(string code)
        {
            NoDiagnostic(code, RuleIds.ReadonlyAttributeNotOnCustomStructs);
        }

        public static IEnumerable<string> ShouldNotWarnTestCases()
        {
            // Can't use attribute on enums
            yield return @"
struct CustomStruct {}
class Foo
{
    [ErrorProne.NET.Annotations.ReadOnlyAttribute]
    public CustomStruct _m;
}";
        }
    }
}
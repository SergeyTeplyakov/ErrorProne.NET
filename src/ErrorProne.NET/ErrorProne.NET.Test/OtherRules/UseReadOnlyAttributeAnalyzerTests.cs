using System.Collections.Generic;
using ErrorProne.NET.Common;
using ErrorProne.NET.Rules.OtherRules;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.Refactorings
{
    [TestFixture]
    public class UseReadOnlyAttributeAnalyzerTests : CSharpAnalyzerTestFixture<DoNotUseReadonlyModifierAnalyzer>
    {
        [TestCaseSource(nameof(ShouldWarnTestCases))]
        public void ShouldWarnTests(string code)
        {
            HasDiagnostic(code, RuleIds.UseReadOnlyAttributeInstead);
        }

        public static IEnumerable<string> ShouldWarnTestCases()
        {
            // Should not warn on large readonly struct
            yield return @"
struct CustomStruct {long l; long l2; long l3; long l3; long l4; long l5; long l6;}
class Foo
{
    [|public readonly CustomStruct _m;|]
}";
        }

        [TestCaseSource(nameof(ShouldNotWarnTestCases))]
        public void ShouldNotWarnIfValid(string code)
        {
            NoDiagnostic(code, RuleIds.UseReadOnlyAttributeInstead);
        }

        public static IEnumerable<string> ShouldNotWarnTestCases()
        {
            // should not warn on readonly primitives
            yield return @"
class Foo
{
    public readonly int _m;
}";

            // No warn on non-readonly fields
            yield return @"
class Foo
{
    public int _m;
}";

            // No warnings on nullable primitives
            yield return @"
class Foo
{
    public readonly int? _m;
}";

            // No warnings on readonly reference types
            yield return @"
class Foo
{
    public readonly string _m;
}";

            // No warnings on enums
            yield return @"
enum CustomEnum {}
class Foo
{
    public readonly CustomEnum _m;
}";
            
            // No warnings on nullable enums
            yield return @"
enum CustomEnum {}
class Foo
{
    public readonly CustomEnum? _m;
}";
            // Should not warn if attribute is already applied
            yield return @"
struct CustomStruct {}
class Foo
{
    [ErrorProne.NET.Annotations.ReadOnlyAttribute]
    public CustomStruct _m;
}";

            // Should not warn if struct is not readonly
            yield return @"
struct CustomStruct {long l1; long l2; long l3; long l4; long l5; long l6;}
class Foo
{
    public CustomStruct _m;
}";
            
            // Should not warn on small structs
            yield return @"
struct CustomStruct {long l1; }
class Foo
{
    public readonly CustomStruct _m;
}";
        }
    }
}
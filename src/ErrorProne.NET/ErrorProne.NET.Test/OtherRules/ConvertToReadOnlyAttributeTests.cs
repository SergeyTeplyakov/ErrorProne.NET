using ErrorProne.NET.Rules.OtherRules;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.OtherRules
{
    [TestFixture]
    public class ConvertToReadOnlyAttributeTests : CSharpCodeFixTestFixture<UseReadOnlyAttributeCodeFixProvider>
    {
        [Test]
        public void WarnOnLargeStruct()
        {
            var code = @"
struct CustomStruct {}
class Foo
{
    public readonly CustomStruct [|_m|];
}";

            var expected = @"
struct CustomStruct {}
class Foo
{
    [ErrorProne.NET.Annotations.ReadOnly]
    public CustomStruct _m;
}";
            this.TestCodeFix(code, expected, DoNotUseReadonlyModifierAnalyzer.Rule);
        }
    }
}
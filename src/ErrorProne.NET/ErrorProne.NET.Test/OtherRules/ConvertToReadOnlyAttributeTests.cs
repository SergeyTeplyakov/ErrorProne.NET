using System.Collections.Generic;
using ErrorProne.NET.Common;
using ErrorProne.NET.Rules.OtherRules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.OtherRules
{
    [TestFixture]
    public class ConvertToReadOnlyAttributeTests : CSharpCodeFixTestFixture<UseReadOnlyAttributeCodeFixProvider>
    {
        [Test]
        public void Convert()
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
            this.TestCodeFix(code, expected, UseReadOnlyAttributeAnalyzer.Rule);
        }
    }
}
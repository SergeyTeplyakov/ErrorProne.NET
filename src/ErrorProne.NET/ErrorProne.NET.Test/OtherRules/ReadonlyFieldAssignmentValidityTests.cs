//using System.Collections.Generic;
//using ErrorProne.NET.Common;
//using ErrorProne.NET.OtherRules;
//using NUnit.Framework;
//using RoslynNunitTestRunner;

//namespace ErrorProne.NET.Test.OtherRules
//{
//    [TestFixture]
//    public class ReadonlyFieldAssignmentTests : CSharpAnalyzerTestFixture<PropertyAssignmentAnalyser>
//    {
//        [TestCaseSource(nameof(ShouldWarnForUnassignedReadOnlyFieldTestCases))]
//        public void ShouldWarnForUnassignedReadOnlyField(string code)
//        {
//            HasDiagnostic(code, RuleIds.ReadonlyFieldWasNeverAssigned);
//        }

//        public static IEnumerable<string> ShouldWarnForUnassignedReadOnlyFieldTestCases()
//        {
//            yield return @"
//class Foo
//{
//	public readonly int [|_m|];
//}";

//            // Should warn on protected fields as well!
//            yield return @"
//class Foo
//{
//	protected readonly string [|_m|];
//}";

//            // Should warn on readonly field with attribute
//            yield return @"
//class Base
//{
//    [ErrorProne.NET.Annotations.ReadOnly]
//    private string _foo;
//}";
//        }

//        [TestCaseSource(nameof(ShouldNotWarnOnUnassignedReadOnlyFieldTestCases))]
//        public void ShouldNotWarnOnUnassignedReadOnlyFields(string code)
//        {
//            NoDiagnostic(code, RuleIds.ReadonlyFieldWasNeverAssigned);
//        }

//        public static IEnumerable<string> ShouldNotWarnOnUnassignedReadOnlyFieldTestCases()
//        {
//            // Should not warn when initialized in constructor
//            yield return @"
//class Foo
//{
//    public Foo(stirng s)
//    {
//       if (s != null) {_m = 42;}
//    }
//	public readonly int _m;
//}";

//            // No warning when initialized inplace
//            yield return @"
//class Foo
//{
//	public static readonly int M = 42;
//}";
//        }
//    }
//}
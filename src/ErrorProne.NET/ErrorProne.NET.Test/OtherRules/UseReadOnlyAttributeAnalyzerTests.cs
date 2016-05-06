using System.Collections.Generic;
using ErrorProne.NET.Common;
using ErrorProne.NET.Rules.OtherRules;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.Refactorings
{
	[TestFixture]
	public class UseReadOnlyAttributeAnalyzerTests : CSharpAnalyzerTestFixture<UseReadOnlyAttributeAnalyzer>
	{
		[TestCaseSource(nameof(ShouldWarnIfInvalidTestCases))]
		public void ShouldWarnIfInvalid(string code)
		{
			HasDiagnostic(code, RuleIds.UseReadOnlyAttributeInstead);
		}

		public static IEnumerable<string> ShouldWarnIfInvalidTestCases()
		{
			// Should not warn on readonly struct
			yield return @"
struct CustomStruct {}
class Foo
{
	[|public readonly CustomStruct _m|];
}";
		}

		[TestCaseSource(nameof(ShouldNotWarnTestCases))]
		public void ShouldNotWarnIfValid(string code)
		{
			NoDiagnostic(code, RuleIds.UseReadOnlyAttributeInstead);
		}

		public static IEnumerable<string> ShouldNotWarnTestCases()
		{
			yield return @"
class Foo
{
	public readonly int _m;
}";

			yield return @"
class Foo
{
	public int _m;
}";

			// Can't use attribute on nullable primitives
			yield return @"
class Foo
{
	public readonly int? _m;
}";

			// Can't use attribute on reference types
			yield return @"
class Foo
{
	public readonly string _m;
}";

			// Can't use attribute on enums
			yield return @"
enum CustomEnum {}
class Foo
{
	public readonly CustomEnum _m;
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
struct CustomStruct {}
class Foo
{
	public CustomStruct _m;
}";
		}
	}
}
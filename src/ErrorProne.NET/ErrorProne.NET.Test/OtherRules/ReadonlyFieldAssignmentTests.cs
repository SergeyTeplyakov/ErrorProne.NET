using System.Collections.Generic;
using ErrorProne.NET.Common;
using ErrorProne.NET.OtherRules;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.OtherRules
{
    [TestFixture]
    public class ReadonlyFieldAssignmentTests : CSharpAnalyzerTestFixture<FieldAnalyzer>
    {
        [TestCaseSource(nameof(ShouldWarnForUnassignedReadOnlyFieldTestCases))]
        public void ShouldWarnForUnassignedReadOnlyField(string code)
        {
            HasDiagnostic(code, RuleIds.ReadonlyFieldWasNeverAssigned);
        }

        public static IEnumerable<string> ShouldWarnForUnassignedReadOnlyFieldTestCases()
        {
            yield return @"
class Foo
{
	public readonly int [|_m|];
}";

            // Should warn on protected fields as well!
            yield return @"
class Foo
{
	protected readonly string [|_m|];
}";

            // Should warn on readonly field with attribute
            yield return @"
class Base
{
    [ErrorProne.NET.Annotations.ReadOnly]
    private string _foo;
}";
        }

        [TestCaseSource(nameof(ShouldNotWarnOnUnassignedReadOnlyFieldTestCases))]
        public void ShouldNotWarnOnUnassignedReadOnlyFields(string code)
        {
            NoDiagnostic(code, RuleIds.ReadonlyFieldWasNeverAssigned);
        }
        
        public static IEnumerable<string> ShouldNotWarnOnUnassignedReadOnlyFieldTestCases()
        {
            // Should not warn for struct fields marked with StructLayoutAttribute
            yield return @"
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal struct VariantPadding
{
    public readonly System.IntPtr Data2;
    public readonly System.IntPtr Data3;
}";
            
            // Should not warn if readonly field was used in another part of partial declaration
            yield return @"
partial class Foo
{
    private readonly int _m;
}

partial class Foo
{
    public Foo()
    {
        _m = 42;
    }
}";

            // Should not warn if readonly field was initialized via out param
            yield return @"
class Foo
{
    public Foo()
    {
       Initialize(out _m);
    }
	public readonly string _m;
    private void Initialize(out string s) {s = null;}
}";
            // Should not warn when initialized in constructor
            yield return @"
class Foo
{
    public Foo(stirng s)
    {
       if (s != null) {_m = 42;}
    }
	public readonly int _m;
}";

            // No warning when initialized inplace
            yield return @"
class Foo
{
	public static readonly int M = 42;
}";
        }

        [TestCaseSource(nameof(ShouldWarnOnReadOnlyFieldAssignmentInInvalidContextTestCases))]
        public void ShouldWarnOnReadOnlyFieldAssignmentInInvalidContext(string code)
        {
            HasDiagnostic(code, RuleIds.ReadOnlyFieldWasAssignedOutsideConstructor);
        }

        private static IEnumerable<string> ShouldWarnOnReadOnlyFieldAssignmentInInvalidContextTestCases()
        {
            yield return @"
class Foo
{
  [ErrorProne.NET.Annotations.ReadOnlyAttribute]
  private int _field;
  public void Test(string arg)
  {
     if (arg != null) {[|_field|] = 42;}
  }
}";
            // Should warn if assignment is happening inside a lambda expression
            yield return @"
class Foo
{
  [ErrorProne.NET.Annotations.ReadOnlyAttribute]
  private int _field;
  public Foo()
  {
     System.Action a = () => {[|_field|] = 42;};
  }
}";
            // Should warn if assignment is happening inside another object
            yield return @"
class Foo
{
  [ErrorProne.NET.Annotations.ReadOnlyAttribute]
  public int _field;
}
class Boo
{
  public void Boo()
  {
    var f = new Foo { [|_field|] = 42; };
  }
}";
        }

        [TestCaseSource(nameof(ShouldNotWarnOnReadOnlyFieldAssignmentInInvalidContextTestCases))]
        public void ShouldNotWarnOnReadOnlyFieldAssignmentInInvalidContext(string code)
        {
            NoDiagnostic(code, RuleIds.ReadOnlyFieldWasAssignedOutsideConstructor);
        }

        private static IEnumerable<string> ShouldNotWarnOnReadOnlyFieldAssignmentInInvalidContextTestCases()
        {
            yield return @"
class Foo
{
  [ErrorProne.NET.Annotations.ReadOnly]
  private int _field = 42;
  public void Test(string arg)
  {
     if (arg != null) {System.Console.WriteLine(_field);}
  }
}
";

            yield return @"
class Foo
{
  [ErrorProne.NET.Annotations.ReadOnly]
  private int _field;
  public Foo() { _field = 42; }
  public void Test(string arg)
  {
     if (arg != null) {System.Console.WriteLine(_field);}
  }
}
";
        }
    }
}
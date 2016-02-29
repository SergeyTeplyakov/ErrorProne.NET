using System.Collections.Generic;
using ErrorProne.NET.Common;
using ErrorProne.NET.OtherRules;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.OtherRules
{
    [TestFixture]
    public class PropertyAssignmentTests : CSharpAnalyzerTestFixture<PropertyAnalyser>
    {
        [TestCaseSource(nameof(ShouldWarnForUnassignedPropertyTestCases))]
        public void ShouldWarnForUnassignedProperty(string code)
        {
            HasDiagnostic(code, RuleIds.ReadonlyPropertyWasNeverAssigned);
        }

        public static IEnumerable<string> ShouldWarnForUnassignedPropertyTestCases()
        {
            yield return @"
class Foo
{
	public int [|M|] { get; }
}";
            // Should warn if property was used but not assigned
            yield return @"
class Foo
{
	public int [|M|] { get; }
    public void Test() { System.Console.WriteLine(M); }
}";

            // Should warn on protected getter!
            yield return @"
class Foo
{
	protected int [|M|] { get; }
}";

            yield return @"
class Base
{
    public virtual string Foo { get; }
}
// Should be warning on the sealed case!!!
class Derived : Base
{
    public sealed override string [|Foo|] { get; }
}";
        }

        [TestCaseSource(nameof(ShouldNotWarnForUnassignedPropertyTestCases))]
        public void ShouldNotWarnForUnassignedProperty(string code)
        {
            NoDiagnostic(code, RuleIds.ReadonlyPropertyWasNeverAssigned);
        }

        public static IEnumerable<string> ShouldNotWarnForUnassignedPropertyTestCases()
        {
            // Should not warn when initialized in constructor
            yield return @"
class Foo
{
    public Foo(stirng s)
    {
       if (s != null) {M = 42;}
    }
	public int M { get; }
}";

            // No warning when initialized inplace
            yield return @"
class Foo
{
	public int M { get; } = 42;
}";
            
            // No warning when property getter is implemented
            yield return @"
class Foo
{
	public int M { get {return 42;} }
}";
            
            // No warning on automatic get-only property
            yield return @"
class Foo
{
	public int M => 42
}";

            // No warning on abstract get-only
            yield return @"
abstract class Foo
{
	public abstract int M { get; } 
}";
            
            // No warning on virtual property.
            // BTW, R# incorrectly emits a warning in this case.
            // But warning should not be there, because derived class can easily provide the value!
            yield return @"
class Base
{
    public virtual string Foo { get; }
}";

            // should not warn when override method is still virtual (i.e. not sealed)
            yield return @"
class Base
{
    public virtual string Foo { get; }
}
class Derived : Base
{
    public override string Foo { get; }
}";
        }

        [Test]
        public void ShouldWarnOnPropertyWithPrivateSetter()
        {
            const string code = @"
class Foo
{
	public int M { get; [|private set;|] }
}";

            HasDiagnostic(code, RuleIds.PropertyWithPrivateSetterWasNeverAssigned);
        }

        [Test]
        public void ShouldNotWarnOnVirtualPropertyWithPrivateSetter()
        {
            const string code = @"
class Foo
{
	public virtual int M { get; private set; }
}";

            NoDiagnostic(code, RuleIds.PropertyWithPrivateSetterWasNeverAssigned);
        }

        [Test]
        public void ShouldWarnOnPrivatePropertyWithPrivateSetter()
        {
            const string code = @"
class Foo
{
	private int M { get; [|set;|] }
}";

            HasDiagnostic(code, RuleIds.PropertyWithPrivateSetterWasNeverAssigned);
        }

        [Test]
        public void ShouldWarnOnPropertyWithPrivateSetterOnEnum()
        {
            const string code = @"
enum Blah {Value = 1;}
class Foo
{
	public Blah M { get; [|private set;|] }
}";

            HasDiagnostic(code, RuleIds.PropertyWithPrivateSetterWasNeverAssigned);
        }

        [Test]
        public void ShouldNotWarnOnPropertyWithPrivateSetterIfInitialized()
        {
            const string code = @"
class Foo
{
    public Foo(stirng s)
    {
       if (s != null) {M = 42;}
    }
	public int M { get; private set; }
}";

            NoDiagnostic(code, RuleIds.PropertyWithPrivateSetterWasNeverAssigned);
        }

        public void ShouldNotWarnOnNonPrivateProperty()
        {
            const string code = @"
class Foo
{
    public Foo(stirng s)
    {
       if (s != null) {M = 42;}
    }
	public int M { get; protected set; }
}";

            NoDiagnostic(code, RuleIds.PropertyWithPrivateSetterWasNeverAssigned);
        }
    }
}
using ErrorProne.NET.Common;
using ErrorProne.NET.OtherRules;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.OtherRules
{
    [TestFixture]
    public class PropertyAssignmentAnalyzer : CSharpAnalyzerTestFixture<PropertyAssignmentAnalyser>
    {
        [Test]
        public void ShouldWarnOnReadonlyProperty()
        {
            const string code = @"
class Foo
{
	public int [|M|] { get; }
}";

            HasDiagnostic(code, RuleIds.ReadonlyPropertyWasNeverAssignmed);
        }

        [Test]
        public void ShouldNotWarnIfInitialized()
        {
            const string code = @"
class Foo
{
    public Foo(stirng s)
    {
       if (s != null) {M = 42;}
    }
	public int M { get; }
}";

            NoDiagnostic(code, RuleIds.ReadonlyPropertyWasNeverAssignmed);
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
using ErrorProne.NET.Core.CoreAnalyzers;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Core.Tests.CoreAnalyzersTests
{
    [TestFixture]
    public class UnobservedResultAnalyzerTests : CSharpAnalyzerTestFixture<UnobservedResultAnalyzer>
    {
        public const string DiagnosticId = UnobservedResultAnalyzer.DiagnosticId;

        [Test]
        public void Method_That_Returns_Exception_Should_Be_Observed()
        {
            string code = @"
class FooBar
{
    public static System.Exception Foo() => null;

    public static void Test()
    {
        [|Foo|]();
    }
}
";
            HasDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void Method_That_Returns_Result_Should_Be_Observed()
        {
            string code = @"
class Result {}

class FooBar
{
    public static Result Foo() => null;

    public static void Test()
    {
        [|Foo|]();
    }
}
";
            HasDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void Method_That_Returns_ResultBase_Should_Be_Observed()
        {
            string code = @"
class ResultBase {}
class MyValue : ResultBase {}

class FooBar
{
    public static MyValue Foo() => null;

    public static void Test()
    {
        [|Foo|]();
    }
}
";
            HasDiagnostic(code, DiagnosticId);
        }

        [Test]
        public void Method_That_Returns_Possible_Should_Be_Observed()
        {
            string code = @"
struct Possible<T> {}

class FooBar
{
    public static Possible<int> Foo() => default;

    public static void Test()
    {
        [|Foo|]();
    }
}
";
            HasDiagnostic(code, DiagnosticId);
        }
    }
}
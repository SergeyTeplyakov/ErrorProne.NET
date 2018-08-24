using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace SwitchAnalyzer.Test
{
    [TestClass]
    public class ClassSwitchAnalyzerTests : CodeFixVerifier
    {
        private readonly string codeStart = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
    
    enum TestEnum
    {
        Case1,
        Case2,
        Case3
    }

    public class TestClass: ITestInterface
    {
            public TestEnum TestMethod()
            {
                var testValue = TestEnum.Case1;";

        private readonly string codeEnd = @"
            }
        }    

    class BaseClass
    {
        public string Foo { get; set; }
    }
    class TestClass2 : BaseClass
    {
        public int Bar { get; set; }
    }
    class TestClass3: BaseClass
    {
        public double Baz { get; set; }
    }
    class TestClass4: BaseClass
    {
        public double Baz { get; set; }
    }
    abstract class AbstractClass: BaseClass
    {
    }
    }";

        [TestMethod]
        public void SimpleValid()
        {
            var switchStatement = @"
            BaseClass test = new TestClass2();
            switch (test)
            {
                case TestClass2 a: return TestEnum.Case1;
                case TestClass3 a: return TestEnum.Case2;
                case TestClass4 a: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SimpleInvalid()
        {
            var switchStatement = @"
            BaseClass test = new TestClass2();
            switch (test)
            {
                case TestClass2 a: return TestEnum.Case1;
                case TestClass4 a: return TestEnum.Case2;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("TestClass3"));
        }

        [TestMethod]
        public void SameClassNotChecked()
        {
            var switchStatement = @"
            BaseClass test = new TestClass2();
            switch (test)
            {
                case BaseClass a: return TestEnum.Case2;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void MultipleValuesReturnedInDiagnostics()
        {
            var switchStatement = @"
            BaseClass test = new TestClass2();
            switch (test)
            {
                case TestClass2 a: return TestEnum.Case1;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("TestClass3", "TestClass4"));
        }

        [TestMethod]
        public void EmptyExpressionValid()
        {
            var switchStatement = @"
            BaseClass test = new TestClass2();
            switch (test)
            {
                case TestClass2 a: return TestEnum.Case1;
                case TestClass3 a:
                case TestClass4 a: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void FixSimple()
        {
            var switchStatement = @"
            BaseClass test = new TestClass2();
            switch (test)
            {
                case TestClass3 a: return TestEnum.Case2;
                case TestClass4 a: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("TestClass2"));


            var expectedFixSwitch = @"
            BaseClass test = new TestClass2();
            switch (test)
            {
                case TestClass3 a: return TestEnum.Case2;
                case TestClass4 a: return TestEnum.Case3;
                case TestClass2 _:
                default: throw new NotImplementedException();
            }";
            var expectedResult = $@"{codeStart}
                          {expectedFixSwitch}
                          {codeEnd}";

            VerifyCSharpFix(test, expectedResult);
        }

        [TestMethod]
        public void FixMoreThanOneValue()
        {
            var switchStatement = @"
            BaseClass test = new TestClass2();
            switch (test)
            {
                case TestClass3 a: return TestEnum.Case2;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("TestClass2", "TestClass4"));

            var expectedFixSwitch = @"
            BaseClass test = new TestClass2();
            switch (test)
            {
                case TestClass3 a: return TestEnum.Case2;
                case TestClass2 _:
                case TestClass4 _:
                default: throw new NotImplementedException();
            }";
            var expectedResult = $@"{codeStart}
                          {expectedFixSwitch}
                          {codeEnd}";

            VerifyCSharpFix(test, expectedResult);
        }

        private DiagnosticResult GetDiagnostic(params string[] expectedTypes)
        {
            return new DiagnosticResult
            {
                Id = "SA003",
                Message = String.Format("Switch case should check implementation of type(s): {0}", string.Join(", ", expectedTypes)),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 26, 13)
                    }
            };
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new SwitchAnalyzer();
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new SwitchAnalyzerCodeFixProvider();
        }
    }
}

using NUnit.Framework;
using RoslynNunitTestRunner;

namespace SwitchAnalyzer.Tests
{
    public class ClassSwitchFixerTests : CSharpCodeFixTestFixture<SwitchAnalyzerCodeFixProvider>
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

        [Test]
        public void FixSimple()
        {
            var switchStatement = @"
            BaseClass test = new TestClass2();
            [|switch (test)
            {
                case TestClass3 a: return TestEnum.Case2;
                case TestClass4 a: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }|]";
            var code = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

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

            TestCodeFix(code, expectedResult, EnumAnalyzer.Rule, MissingCases("TestClass2"));
        }

        [Test]
        public void FixMoreThanOneValue()
        {
            var switchStatement = @"
            BaseClass test = new TestClass2();
            [|switch (test)
            {
                case TestClass3 a: return TestEnum.Case2;
                default: throw new NotImplementedException();
            }|]";
            var code = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

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

            TestCodeFix(code, expectedResult, EnumAnalyzer.Rule, MissingCases("TestClass2", "TestClass4"));
        }

        private string MissingCases(params string[] cases) => string.Join(", ", cases);
    }
}

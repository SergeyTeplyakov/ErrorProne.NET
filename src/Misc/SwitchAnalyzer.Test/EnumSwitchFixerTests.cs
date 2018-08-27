using NUnit.Framework;
using RoslynNunitTestRunner;

namespace SwitchAnalyzer.Tests
{
    [TestFixture]
    public class EnumSwitchFixerTests: CSharpCodeFixTestFixture<SwitchAnalyzerCodeFixProvider>
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

        class TestClass
        {
            public TestEnum TestMethod()
            {
                var testValue = TestEnum.Case1;";

        private readonly string codeEnd =
            @"
            }}

            public class NotImplementedExceptionInheritor : NotImplementedException
            {{
            }}
            private TestEnum GetEnum(TestEnum enumValue)
            {{
                return enumValue;
            }}
        }}
    {0}
    enum EnumWithDuplicates
    {{
        Case1 = 1,
        Case2 = 2,
        DuplicateCase = 1
    }}
    }}
namespace OtherNamespace
    {{
        enum OtherEnum
    {{
        Case1,
        Case2,
        Case3
    }}
    enum TestEnum
    {{
        OtherNamespaceCase1,
        OtherNamespaceCase2,
        OtherNamespaceCase3
    }}
    }}";

        [Test]
        public void FixSimple()
        {
            var switchStatement = @"
            [|switch (TestEnum.Case1)
            {
                case TestEnum.Case2: return TestEnum.Case2;
                case TestEnum.Case3: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }|]";
            var code = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            var expectedFixSwitch = @"
            switch (TestEnum.Case1)
            {
                case TestEnum.Case2: return TestEnum.Case2;
                case TestEnum.Case3: return TestEnum.Case3;
                case TestEnum.Case1:
                default: throw new NotImplementedException();
            }";
            var expectedResult = $@"{codeStart}
                          {expectedFixSwitch}
                          {GetEndSection()}";

            TestCodeFix(code, expectedResult, EnumAnalyzer.Rule, MissingCases("TestEnum.Case1"));
        }

        [Test]
        public void FixManyCases()
        {
            var switchStatement = @"
            [|switch (TestEnum.Case1)
            {
                default: throw new NotImplementedException();
            }|]";
            var code = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            var expectedFixSwitch = @"
            switch (TestEnum.Case1)
            {
                case TestEnum.Case1:
                case TestEnum.Case2:
                case TestEnum.Case3:
                default: throw new NotImplementedException();
            }";
            var expectedResult = $@"{codeStart}
                          {expectedFixSwitch}
                          {GetEndSection()}";

            TestCodeFix(code, expectedResult, EnumAnalyzer.Rule, MissingCases("TestEnum.Case1", "TestEnum.Case2", "TestEnum.Case3"));
        }

        [Test]
        public void FixWithoutDefault1()
        {
            var switchStatement = @"
            [|switch (TestEnum.Case1)
            {
            }|]
            return TestEnum.Case1;";
            var code = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            var expectedFixSwitch = @"
            switch (TestEnum.Case1)
            {
                case TestEnum.Case1:
                case TestEnum.Case2:
                case TestEnum.Case3:
                    {
                        break;
                    }
            }
            return TestEnum.Case1;";
            var expectedResult = $@"{codeStart}
                          {expectedFixSwitch}
                          {GetEndSection()}";

            TestCodeFix(code, expectedResult, EnumAnalyzer.Rule, MissingCases("TestEnum.Case1", "TestEnum.Case2", "TestEnum.Case3"));
        }

        [Test]
        public void FixWithoutDefault2()
        {
            var switchStatement = @"
            [|switch (testValue)
            {
                case TestEnum.Case1:
                    {
                        var k = 3;
                        break;
                    }
            }|]
            return TestEnum.Case1;";
            var code = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            var expectedFixSwitch = @"
            switch (testValue)
            {
                case TestEnum.Case1:
                    {
                        var k = 3;
                        break;
                    }

                case TestEnum.Case2:
                case TestEnum.Case3:
                    {
                        break;
                    }
            }
            return TestEnum.Case1;";
            var expectedResult = $@"{codeStart}
                          {expectedFixSwitch}
                          {GetEndSection()}";

            TestCodeFix(code, expectedResult, EnumAnalyzer.Rule, MissingCases("TestEnum.Case2", "TestEnum.Case3"));
        }

        [Test]
        public void FixWithNamespace()
        {
            var switchStatement = @"
            [|switch (OtherNamespace.OtherEnum.Case1)
            {
                case OtherNamespace.OtherEnum.Case1: return TestEnum.Case1;
                case OtherNamespace.OtherEnum.Case2: return TestEnum.Case2;
                default: throw new NotImplementedException();
            }|]";
            var code = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            var expectedFixSwitch = @"
            switch (OtherNamespace.OtherEnum.Case1)
            {
                case OtherNamespace.OtherEnum.Case1: return TestEnum.Case1;
                case OtherNamespace.OtherEnum.Case2: return TestEnum.Case2;
                case OtherNamespace.OtherEnum.Case3:
                default: throw new NotImplementedException();
            }";
            var expectedResult = $@"{codeStart}
                          {expectedFixSwitch}
                          {GetEndSection()}";

            TestCodeFix(code, expectedResult, EnumAnalyzer.Rule, MissingCases("OtherNamespace.OtherEnum.Case3"));
        }

        private string GetEndSection(string additionalCode = "") => string.Format(codeEnd, additionalCode);

        private string MissingCases(params string[] cases) => string.Join(", ", cases);
    }
}

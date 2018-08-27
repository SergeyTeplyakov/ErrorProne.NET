using NUnit.Framework;
using RoslynNunitTestRunner;

namespace SwitchAnalyzer.Tests
{
    [TestFixture]
    public class InterfaceSwitchFixerTests : CSharpCodeFixTestFixture<SwitchAnalyzerCodeFixProvider>
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
            ITestInterface test = new TestClass();
            [|switch (test)
            {
                case TestClass a: return TestEnum.Case2;
                case IChildInterface i: return TestEnum.Case1;
                default: throw new NotImplementedException();
            }|]";
            var code = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            var expectedFixSwitch = @"
            ITestInterface test = new TestClass();
            switch (test)
            {
                case TestClass a: return TestEnum.Case2;
                case IChildInterface i: return TestEnum.Case1;
                case OneMoreInheritor _:
                default: throw new NotImplementedException();
            }";
            var expectedResult = $@"{codeStart}
                          {expectedFixSwitch}
                          {codeEnd}";

            TestCodeFix(code, expectedResult, InterfaceAnalyzer.Rule, MissingCases("OneMoreInheritor"));
        }

        [Test]
        public void FixInterface()
        {
            var switchStatement = @"
            ITestInterface test = new TestClass();
            [|switch (test)
            {
                case TestClass a: return TestEnum.Case2;
                case OneMoreInheritor o: return TestEnum.Case1;
                default: throw new NotImplementedException();
            }|]";
            var code = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            var expectedFixSwitch = @"
            ITestInterface test = new TestClass();
            switch (test)
            {
                case TestClass a: return TestEnum.Case2;
                case OneMoreInheritor o: return TestEnum.Case1;
                case IChildInterface _:
                default: throw new NotImplementedException();
            }";
            var expectedResult = $@"{codeStart}
                          {expectedFixSwitch}
                          {codeEnd}";

            TestCodeFix(code, expectedResult, InterfaceAnalyzer.Rule, MissingCases("IChildInterface"));
        }

        private string MissingCases(params string[] cases) => string.Join(", ", cases);
    }
}

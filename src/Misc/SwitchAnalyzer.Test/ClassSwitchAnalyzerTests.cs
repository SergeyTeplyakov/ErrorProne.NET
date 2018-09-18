using NUnit.Framework;
using RoslynNunitTestRunner;

namespace SwitchAnalyzer.Tests
{
    [TestFixture]
    public class ClassSwitchAnalyzerTests : CSharpAnalyzerTestFixture<SwitchAnalyzer>
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
            NoDiagnostic(test, EnumAnalyzer.DiagnosticId);
        }

        [Test]
        public void SimpleInvalid()
        {
            var switchStatement = @"
            BaseClass test = new TestClass2();
            [|switch (test)
            {
                case TestClass2 a: return TestEnum.Case1;
                case TestClass4 a: return TestEnum.Case2;
                default: throw new NotImplementedException();
            }|]";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            HasDiagnostic(test, ClassAnalyzer.DiagnosticId, AnalyzerMessage("TestClass3"));
        }

        [Test]
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

            NoDiagnostic(test, ClassAnalyzer.DiagnosticId);
        }

        [Test]
        public void MultipleValuesReturnedInDiagnostics()
        {
            var switchStatement = @"
            BaseClass test = new TestClass2();
            [|switch (test)
            {
                case TestClass2 a: return TestEnum.Case1;
                default: throw new NotImplementedException();
            }|]";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            HasDiagnostic(test, ClassAnalyzer.DiagnosticId, AnalyzerMessage("TestClass3", "TestClass4"));
        }

        [Test]
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

            NoDiagnostic(test, ClassAnalyzer.DiagnosticId);
        }

        private string AnalyzerMessage(params string[] cases) => string.Format(ClassAnalyzer.Rule.MessageFormat.ToString(), string.Join(", ", cases));
    }
}

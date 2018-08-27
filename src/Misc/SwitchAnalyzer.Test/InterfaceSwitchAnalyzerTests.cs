using NUnit.Framework;
using RoslynNunitTestRunner;

namespace SwitchAnalyzer.Tests
{
    [TestFixture]
    public class InterfaceSwitchAnalyzerTests : CSharpAnalyzerTestFixture<SwitchAnalyzer>
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

    public interface ITestInterface
    {
    }

        public class TestClass: ITestInterface
        {
            public TestEnum TestMethod()
            {
                var testValue = TestEnum.Case1;";

        private readonly string codeEnd = @"
            }

            public class OneMoreInheritor : ITestInterface
            {
            }
            public interface IChildInterface: ITestInterface
            {
            }
            public interface IGrandChild: IChildInterface
            {
            }
            private TestEnum GetEnum(TestEnum enumValue)
            {
                return enumValue;
            }
        }
    }
namespace OtherNamespace
{
    public interface ITestInterface
    {
    }
    public class OneMoreInheritor : ITestInterface
    {
    }
    public interface IChildInterface: ITestInterface
    {
    }
}";

        [Test]
        public void SimpleValid()
        {
            var switchStatement = @"
            ITestInterface test = new TestClass();
            switch (test)
            {
                case TestClass a: return TestEnum.Case1;
                case OneMoreInheritor a: return TestEnum.Case2;
                case IChildInterface i: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            NoDiagnostic(test, InterfaceAnalyzer.DiagnosticId);
        }

        [Test]
        public void SimpleInvalid()
        {
            var switchStatement = @"
            ITestInterface test = new TestClass();
            [|switch (test)
            {
                case TestClass a: return TestEnum.Case1;
                default: throw new NotImplementedException();
            }|]";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            HasDiagnostic(test, InterfaceAnalyzer.DiagnosticId, AnalyzerMessage("IChildInterface", "OneMoreInheritor"));
        }

        [Test]
        public void CheckWithThrowInBlock()
        {
            var switchStatement = @"
            ITestInterface test = new TestClass();
            [|switch (test)
            {
                case TestClass a: return TestEnum.Case1;
                case IChildInterface i: return TestEnum.Case2;
                default: default:{
                        var s = GetEnum(testValue);
                        throw new NotImplementedException();
                        }
            }|]";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            HasDiagnostic(test, InterfaceAnalyzer.DiagnosticId, AnalyzerMessage("OneMoreInheritor"));
        }

        [Test]
        public void NoChecksWithoutThrowInDefault()
        {
            var switchStatement = @"
            ITestInterface test = new TestClass();
            switch (test)
            {
                case TestClass a: return TestEnum.Case1;
                default: default:{
                        var s = GetEnum(testValue);
                        break;
                        }
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            NoDiagnostic(test, InterfaceAnalyzer.DiagnosticId);
        }

        [Test]
        public void MultipleValuesReturnedInDiagnostic()
        {
            var switchStatement = @"
            ITestInterface test = new TestClass();
            [|switch (test)
            {
                default: default:{
                        var s = GetEnum(testValue);
                        throw new NotImplementedException();
                        }
            }|]";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            HasDiagnostic(test, InterfaceAnalyzer.DiagnosticId, AnalyzerMessage("IChildInterface", "OneMoreInheritor", "TestClass"));
        }

        [Test]
        public void ArgumentAsTypeConversionValid()
        {
            var switchStatement = @"
            ITestInterface test = new TestClass();
            [|switch (new TestClass() as ITestInterface)
            {
                case TestClass a: return TestEnum.Case1;
                case IChildInterface i: return TestEnum.Case2;
                default: throw new NotImplementedException();
            }|]";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            HasDiagnostic(test, InterfaceAnalyzer.DiagnosticId, AnalyzerMessage("OneMoreInheritor"));
        }

        [Test]
        public void EmptyExpressionValid()
        {
            var switchStatement = @"
            ITestInterface test = new TestClass();
            switch (test)
            {
                case TestClass a:
                case OneMoreInheritor a: return TestEnum.Case2;
                case IChildInterface i:
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            NoDiagnostic(test, InterfaceAnalyzer.DiagnosticId);
        }

        [Test]
        public void DontCheckFromOtherNamespace()
        {
            var switchStatement = @"
            OtherNamespace.ITestInterface test = new OtherNamespace.TestClass();
            switch (test)
            {
                case OtherNamespace.OneMoreInheritor o: return TestEnum.Case1;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {codeEnd}";

            // No check for items from other namespaces not referenced in current place.
            NoDiagnostic(test, InterfaceAnalyzer.DiagnosticId);
        }

        private string AnalyzerMessage(params string[] cases) => string.Format(InterfaceAnalyzer.Rule.MessageFormat.ToString(), string.Join(", ", cases));
    }
}

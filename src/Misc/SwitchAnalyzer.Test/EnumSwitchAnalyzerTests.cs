using NUnit.Framework;
using RoslynNunitTestRunner;

namespace SwitchAnalyzer.Tests
{
    [TestFixture]
    public class EnumSwitchAnalyzerTests : CSharpAnalyzerTestFixture<SwitchAnalyzer>
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

        private string GetEndSection(string additionalCode = "") => string.Format(codeEnd, additionalCode);

        //No diagnostics expected to show up
        [Test]
        public void EmptyValid()
        {
            var test = @"";

            NoDiagnostic(test, EnumAnalyzer.DiagnosticId);
        }

        [Test]
        public void SimpleValid()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case TestEnum.Case1: return TestEnum.Case1;
                case TestEnum.Case2: return TestEnum.Case2;
                case TestEnum.Case3: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            NoDiagnostic(test, EnumAnalyzer.DiagnosticId);
        }

        [Test]
        public void ValidWithOtherNamespace()
        {
            var switchStatement = @"
            [|switch (OtherNamespace.TestEnum.OtherNamespaceCase1)
            {
                case OtherNamespace.TestEnum.OtherNamespaceCase1: return TestEnum.Case1;
                case OtherNamespace.TestEnum.OtherNamespaceCase2: return TestEnum.Case2;
                default: throw new NotImplementedException();
            }|]";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            HasDiagnostic(test, EnumAnalyzer.DiagnosticId, AnalyzerMessage("OtherNamespace.TestEnum.OtherNamespaceCase3"));
        }

        [Test]
        public void ValidWithNamespace()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case ConsoleApplication1.TestEnum.Case1: return TestEnum.Case1;
                case ConsoleApplication1.TestEnum.Case2: return TestEnum.Case2;
                case ConsoleApplication1.TestEnum.Case3: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            NoDiagnostic(test, EnumAnalyzer.DiagnosticId);
        }

        [Test]
        public void OtherEnumTypeValid()
        {
            var enumWithOtherType = @"
            enum OtherTypeEnum: {0}
            {{
                Case1 = 1,
                Case2 = 2
            }}";

            var switchStatement = @"
            [|switch (OtherTypeEnum.Case1)
            {
                case OtherTypeEnum.Case1: return TestEnum.Case1;
                default: throw new NotImplementedException();
            }|]";

            var types = new[] { "int", "uint", "short", "ushort", "byte", "sbyte", "long", "ulong" };
            foreach (var typeName in types)
            {
                
                var substitutedEnum = string.Format(enumWithOtherType, typeName);

                var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection(substitutedEnum)}";

                HasDiagnostic(test, EnumAnalyzer.DiagnosticId, AnalyzerMessage("OtherTypeEnum.Case2"));
            }
        }

        [Test]
        public void SimpleWithNamespace()
        {
            var switchStatement = @"
            [|switch (TestEnum.Case1)
            {
                case ConsoleApplication1.TestEnum.Case1: return TestEnum.Case1;
                case ConsoleApplication1.TestEnum.Case2: return TestEnum.Case2;
                default: throw new NotImplementedException();
            }|]";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            HasDiagnostic(test, EnumAnalyzer.DiagnosticId, AnalyzerMessage("TestEnum.Case3"));
        }

        [Test]
        public void ChecksWithThrowInBlock()
        {
            var switchStatement = @"
            [|switch (testValue)
            {
                case TestEnum.Case2: return TestEnum.Case2;
                case TestEnum.Case3: return TestEnum.Case3;
                default:{
                        var s = GetEnum(testValue);
                        throw new ArgumentException();
                        }
            }|]";

            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            HasDiagnostic(test, EnumAnalyzer.DiagnosticId, AnalyzerMessage("TestEnum.Case1"));
        }

        [Test]
        public void NoChecksWithoutThrowInDefault()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case TestEnum.Case2: {break;}
                case TestEnum.Case3: {break;}
                default: {
                break;
                }
            }
            return TestEnum.Case2;";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            NoDiagnostic(test, EnumAnalyzer.DiagnosticId);
        }

        [Test]
        public void MultipleValuesReturnedInDiagnostic()
        {
            var switchStatement = @"
            [|switch (TestEnum.Case1)
            {
                case TestEnum.Case2: return TestEnum.Case2;
                default: throw new NotImplementedException();
            }|]";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            HasDiagnostic(test, EnumAnalyzer.DiagnosticId, AnalyzerMessage("TestEnum.Case1", "TestEnum.Case3"));
        }

        [Test]
        public void ArgumentAsMethodCallValid()
        {
            var switchStatement = @"
            switch (GetEnum(testValue))
            {
                case TestEnum.Case1: return TestEnum.Case1;
                case TestEnum.Case2: return TestEnum.Case2;
                case TestEnum.Case3: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            NoDiagnostic(test, EnumAnalyzer.DiagnosticId);
        }

        [Test]
        public void BItwiseOrValid()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case TestEnum.Case1 | TestEnum.Case2: return TestEnum.Case1;
                case TestEnum.Case3: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            NoDiagnostic(test, EnumAnalyzer.DiagnosticId);
        }

        [Test]
        public void BitwiseAndInvalid()
        {
            var switchStatement = @"
            [|switch (TestEnum.Case1)
            {
                case TestEnum.Case1 & TestEnum.Case2: return TestEnum.Case1;
                case TestEnum.Case3: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }|]";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            HasDiagnostic(test, EnumAnalyzer.DiagnosticId, AnalyzerMessage("TestEnum.Case1", "TestEnum.Case2"));
        }

        [Test]
        public void BitwiseAndSameResultValid()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case TestEnum.Case1 & TestEnum.Case1: return TestEnum.Case1;
                case TestEnum.Case2: return TestEnum.Case3;
                case TestEnum.Case3: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            NoDiagnostic(test, EnumAnalyzer.DiagnosticId);
        }

        [Test]
        public void ComplexBitwiseCaseValid()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case (TestEnum.Case1 & TestEnum.Case1) | (TestEnum.Case2 | TestEnum.Case3): return TestEnum.Case1;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            NoDiagnostic(test, EnumAnalyzer.DiagnosticId);
        }

        [Test]
        public void ComplexBitwiseCaseInvalid()
        {
            var switchStatement = @"
            [|switch (TestEnum.Case1)
            {
                case (TestEnum.Case1 & TestEnum.Case1) | (TestEnum.Case2 & TestEnum.Case3): return TestEnum.Case1;
                default: throw new NotImplementedException();
            }|]";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            HasDiagnostic(test, EnumAnalyzer.DiagnosticId, AnalyzerMessage("TestEnum.Case2", "TestEnum.Case3"));
        }

        [Test]
        public void EmptyExpressionValid()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case TestEnum.Case1: return TestEnum.Case1;
                case TestEnum.Case2:
                case TestEnum.Case3: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            NoDiagnostic(test, EnumAnalyzer.DiagnosticId);
        }

        [Test]
        public void DuplicateCasesAreNotCheckedTwice()
        {
            var switchStatement = @"
            switch (EnumWithDuplicates.Case1)
            {
                case EnumWithDuplicates.Case1: return TestEnum.Case1;
                case EnumWithDuplicates.Case2: return TestEnum.Case2:
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            NoDiagnostic(test, EnumAnalyzer.DiagnosticId);
        }

        private string AnalyzerMessage(params string[] cases) => string.Format(EnumAnalyzer.Rule.MessageFormat.ToString(), string.Join(", ", cases));
    }
}

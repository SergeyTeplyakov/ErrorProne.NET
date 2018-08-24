using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Microsoft.CodeAnalysis.CodeFixes;
using TestHelper;

namespace SwitchAnalyzer.Test
{
    [TestClass]
    public class EnumSwitchAnalyzerTests : CodeFixVerifier
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
        [TestMethod]
        public void EmptyValid()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
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

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ValidWithOtherNamespace()
        {
            var switchStatement = @"
            switch (OtherNamespace.TestEnum.OtherNamespaceCase1)
            {
                case OtherNamespace.TestEnum.OtherNamespaceCase1: return TestEnum.Case1;
                case OtherNamespace.TestEnum.OtherNamespaceCase2: return TestEnum.Case2;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("OtherNamespace.TestEnum.OtherNamespaceCase3"));
        }

        [TestMethod]
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

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void OtherEnumTypeValid()
        {
            var enumWithOtherType = @"
            enum OtherTypeEnum: {0}
            {{
                Case1 = 1,
                Case2 = 2
            }}";

            var switchStatement = @"
            switch (OtherTypeEnum.Case1)
            {
                case OtherTypeEnum.Case1: return TestEnum.Case1;
                default: throw new NotImplementedException();
            }";

            var types = new[] { "int", "uint", "short", "ushort", "byte", "sbyte", "long", "ulong" };
            foreach (var typeName in types)
            {
                
                var substitutedEnum = string.Format(enumWithOtherType, typeName);

                var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection(substitutedEnum)}";

                VerifyCSharpDiagnostic(test, GetDiagnostic("OtherTypeEnum.Case2"));
            }
        }

        [TestMethod]
        public void SimpleWithNamespace()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case ConsoleApplication1.TestEnum.Case1: return TestEnum.Case1;
                case ConsoleApplication1.TestEnum.Case2: return TestEnum.Case2;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("TestEnum.Case3"));
        }

        [TestMethod]
        public void ChecksWithThrowInBlock()
        {
            var switchStatement = @"
            switch (testValue)
            {
                case TestEnum.Case2: return TestEnum.Case2;
                case TestEnum.Case3: return TestEnum.Case3;
                default:{
                        var s = GetEnum(testValue);
                        throw new ArgumentException();
                        }
            }";

            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("TestEnum.Case1"));
        }

        [TestMethod]
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

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void MultipleValuesReturnedInDiagnostic()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case TestEnum.Case2: return TestEnum.Case2;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("TestEnum.Case1", "TestEnum.Case3"));
        }

        [TestMethod]
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

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
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

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void BitwiseAndInvalid()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case TestEnum.Case1 & TestEnum.Case2: return TestEnum.Case1;
                case TestEnum.Case3: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("TestEnum.Case1, TestEnum.Case2"));
        }

        [TestMethod]
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

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
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

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ComplexBitwiseCaseInvalid()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case (TestEnum.Case1 & TestEnum.Case1) | (TestEnum.Case2 & TestEnum.Case3): return TestEnum.Case1;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("TestEnum.Case2", "TestEnum.Case3"));
        }

        [TestMethod]
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

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
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

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void FixSimple()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                case TestEnum.Case2: return TestEnum.Case2;
                case TestEnum.Case3: return TestEnum.Case3;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("TestEnum.Case1"));


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

            VerifyCSharpFix(test, expectedResult);
        }

        [TestMethod]
        public void FixManyCases()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("TestEnum.Case1", "TestEnum.Case2", "TestEnum.Case3"));


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

            VerifyCSharpFix(test, expectedResult);
        }

        [TestMethod]
        public void FixWithoutDefault1()
        {
            var switchStatement = @"
            switch (TestEnum.Case1)
            {
            }
            return TestEnum.Case1;";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("TestEnum.Case1", "TestEnum.Case2", "TestEnum.Case3"));


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

            VerifyCSharpFix(test, expectedResult);
        }

        [TestMethod]
        public void FixWithoutDefault2()
        {
            var switchStatement = @"
            switch (testValue)
            {
                case TestEnum.Case1:
                    {
                        var k = 3;
                        break;
                    }
            }
            return TestEnum.Case1;";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("TestEnum.Case2", "TestEnum.Case3"));


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
            VerifyCSharpFix(test, expectedResult);
        }

        [TestMethod]
        public void FixWithNamespace()
        {
            var switchStatement = @"
            switch (OtherNamespace.OtherEnum.Case1)
            {
                case OtherNamespace.OtherEnum.Case1: return TestEnum.Case1;
                case OtherNamespace.OtherEnum.Case2: return TestEnum.Case2;
                default: throw new NotImplementedException();
            }";
            var test = $@"{codeStart}
                          {switchStatement}
                          {GetEndSection()}";

            VerifyCSharpDiagnostic(test, GetDiagnostic("OtherNamespace.OtherEnum.Case3"));

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
            VerifyCSharpFix(test, expectedResult);
        }

        private DiagnosticResult GetDiagnostic(params string[] expectedEnums)
        {
            return new DiagnosticResult
            {
                Id = "SA001",
                Message = String.Format("Switch case should check enum value(s): {0}", string.Join(", ", expectedEnums)),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 25, 13)
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

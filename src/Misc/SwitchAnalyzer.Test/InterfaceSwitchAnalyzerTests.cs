//using System;
//using Microsoft.CodeAnalysis;
//using Microsoft.CodeAnalysis.CodeFixes;
//using Microsoft.CodeAnalysis.Diagnostics;
//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using TestHelper;

//namespace SwitchAnalyzer.Test
//{
//    [TestClass]
//    public class InterfaceSwitchAnalyzerTests : CodeFixVerifier
//    {
//        private readonly string codeStart = @"
//    using System;
//    using System.Collections.Generic;
//    using System.Linq;
//    using System.Text;
//    using System.Threading.Tasks;
//    using System.Diagnostics;

//    namespace ConsoleApplication1
//    {
    
//    enum TestEnum
//    {
//        Case1,
//        Case2,
//        Case3
//    }

//    public interface ITestInterface
//    {
//    }

//        public class TestClass: ITestInterface
//        {
//            public TestEnum TestMethod()
//            {
//                var testValue = TestEnum.Case1;";

//        private readonly string codeEnd = @"
//            }

//            public class OneMoreInheritor : ITestInterface
//            {
//            }
//            public interface IChildInterface: ITestInterface
//            {
//            }
//            public interface IGrandChild: IChildInterface
//            {
//            }
//            private TestEnum GetEnum(TestEnum enumValue)
//            {
//                return enumValue;
//            }
//        }
//    }
//namespace OtherNamespace
//{
//    public interface ITestInterface
//    {
//    }
//    public class OneMoreInheritor : ITestInterface
//    {
//    }
//    public interface IChildInterface: ITestInterface
//    {
//    }
//}";

//        [TestMethod]
//        public void SimpleValid()
//        {
//            var switchStatement = @"
//            ITestInterface test = new TestClass();
//            switch (test)
//            {
//                case TestClass a: return TestEnum.Case1;
//                case OneMoreInheritor a: return TestEnum.Case2;
//                case IChildInterface i: return TestEnum.Case3;
//                default: throw new NotImplementedException();
//            }";
//            var test = $@"{codeStart}
//                          {switchStatement}
//                          {codeEnd}";

//            VerifyCSharpDiagnostic(test);
//        }

//        [TestMethod]
//        public void SimpleInvalid()
//        {
//            var switchStatement = @"
//            ITestInterface test = new TestClass();
//            switch (test)
//            {
//                case TestClass a: return TestEnum.Case1;
//                default: throw new NotImplementedException();
//            }";
//            var test = $@"{codeStart}
//                          {switchStatement}
//                          {codeEnd}";

//            VerifyCSharpDiagnostic(test, GetDiagnostic("IChildInterface", "OneMoreInheritor"));
//        }

//        [TestMethod]
//        public void CheckWithThrowInBlock()
//        {
//            var switchStatement = @"
//            ITestInterface test = new TestClass();
//            switch (test)
//            {
//                case TestClass a: return TestEnum.Case1;
//                case IChildInterface i: return TestEnum.Case2;
//                default: default:{
//                        var s = GetEnum(testValue);
//                        throw new NotImplementedException();
//                        }
//            }";
//            var test = $@"{codeStart}
//                          {switchStatement}
//                          {codeEnd}";

//            VerifyCSharpDiagnostic(test, GetDiagnostic("OneMoreInheritor"));
//        }

//        [TestMethod]
//        public void NoChecksWithoutThrowInDefault()
//        {
//            var switchStatement = @"
//            ITestInterface test = new TestClass();
//            switch (test)
//            {
//                case TestClass a: return TestEnum.Case1;
//                default: default:{
//                        var s = GetEnum(testValue);
//                        break;
//                        }
//            }";
//            var test = $@"{codeStart}
//                          {switchStatement}
//                          {codeEnd}";

//            VerifyCSharpDiagnostic(test);
//        }

//        [TestMethod]
//        public void MultipleValuesReturnedInDiagnostic()
//        {
//            var switchStatement = @"
//            ITestInterface test = new TestClass();
//            switch (test)
//            {
//                default: default:{
//                        var s = GetEnum(testValue);
//                        throw new NotImplementedException();
//                        }
//            }";
//            var test = $@"{codeStart}
//                          {switchStatement}
//                          {codeEnd}";

//            VerifyCSharpDiagnostic(test, GetDiagnostic("IChildInterface", "OneMoreInheritor", "TestClass"));
//        }

//        [TestMethod]
//        public void ArgumentAsTypeConversionValid()
//        {
//            var switchStatement = @"
//            ITestInterface test = new TestClass();
//            switch (new TestClass() as ITestInterface)
//            {
//                case TestClass a: return TestEnum.Case1;
//                case IChildInterface i: return TestEnum.Case2;
//                default: throw new NotImplementedException();
//            }";
//            var test = $@"{codeStart}
//                          {switchStatement}
//                          {codeEnd}";

//            VerifyCSharpDiagnostic(test, GetDiagnostic("OneMoreInheritor"));
//        }

//        [TestMethod]
//        public void EmptyExpressionValid()
//        {
//            var switchStatement = @"
//            ITestInterface test = new TestClass();
//            switch (test)
//            {
//                case TestClass a:
//                case OneMoreInheritor a: return TestEnum.Case2;
//                case IChildInterface i:
//                default: throw new NotImplementedException();
//            }";
//            var test = $@"{codeStart}
//                          {switchStatement}
//                          {codeEnd}";

//            VerifyCSharpDiagnostic(test);
//        }

//        [TestMethod]
//        public void DontCheckFromOtherNamespace()
//        {
//            var switchStatement = @"
//            OtherNamespace.ITestInterface test = new OtherNamespace.TestClass();
//            switch (test)
//            {
//                case OtherNamespace.OneMoreInheritor o: return TestEnum.Case1;
//                default: throw new NotImplementedException();
//            }";
//            var test = $@"{codeStart}
//                          {switchStatement}
//                          {codeEnd}";

//            // No check for items from other namespaces not referenced in current place.
//            VerifyCSharpDiagnostic(test);
//        }

//        [TestMethod]
//        public void FixSimple()
//        {
//            var switchStatement = @"
//            ITestInterface test = new TestClass();
//            switch (test)
//            {
//                case TestClass a: return TestEnum.Case2;
//                case IChildInterface i: return TestEnum.Case1;
//                default: throw new NotImplementedException();
//            }";
//            var test = $@"{codeStart}
//                          {switchStatement}
//                          {codeEnd}";

//            VerifyCSharpDiagnostic(test, GetDiagnostic("OneMoreInheritor"));


//            var expectedFixSwitch = @"
//            ITestInterface test = new TestClass();
//            switch (test)
//            {
//                case TestClass a: return TestEnum.Case2;
//                case IChildInterface i: return TestEnum.Case1;
//                case OneMoreInheritor _:
//                default: throw new NotImplementedException();
//            }";
//            var expectedResult = $@"{codeStart}
//                          {expectedFixSwitch}
//                          {codeEnd}";

//            VerifyCSharpFix(test, expectedResult);
//        }

//        [TestMethod]
//        public void FixInterface()
//        {
//            var switchStatement = @"
//            ITestInterface test = new TestClass();
//            switch (test)
//            {
//                case TestClass a: return TestEnum.Case2;
//                case OneMoreInheritor o: return TestEnum.Case1;
//                default: throw new NotImplementedException();
//            }";
//            var test = $@"{codeStart}
//                          {switchStatement}
//                          {codeEnd}";

//            VerifyCSharpDiagnostic(test, GetDiagnostic("IChildInterface"));

//            var expectedFixSwitch = @"
//            ITestInterface test = new TestClass();
//            switch (test)
//            {
//                case TestClass a: return TestEnum.Case2;
//                case OneMoreInheritor o: return TestEnum.Case1;
//                case IChildInterface _:
//                default: throw new NotImplementedException();
//            }";
//            var expectedResult = $@"{codeStart}
//                          {expectedFixSwitch}
//                          {codeEnd}";

//            VerifyCSharpFix(test, expectedResult);
//        }

//        private DiagnosticResult GetDiagnostic(params string[] expectedTypes)
//        {
//            return new DiagnosticResult
//            {
//                Id = "SA002",
//                Message = String.Format("Switch case should check interface implementation of type(s): {0}", string.Join(", ", expectedTypes)),
//                Severity = DiagnosticSeverity.Warning,
//                Locations =
//                    new[]
//                    {
//                        new DiagnosticResultLocation("Test0.cs", 30, 13)
//                    }
//            };
//        }

//        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
//        {
//            return new SwitchAnalyzer();
//        }

//        protected override CodeFixProvider GetCSharpCodeFixProvider()
//        {
//            return new SwitchAnalyzerCodeFixProvider();
//        }
//    }
//}

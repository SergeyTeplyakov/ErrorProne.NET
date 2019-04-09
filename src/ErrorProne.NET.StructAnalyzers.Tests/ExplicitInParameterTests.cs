using NUnit.Framework;
using RoslynNUnitTestRunner;
using System.Threading.Tasks;
using VerifyCS = RoslynNUnitTestRunner.CSharpCodeFixVerifier<
    ErrorProne.NET.StructAnalyzers.ExplicitInParameterAnalyzer,
    ErrorProne.NET.StructAnalyzers.ExplicitInParameterCodeFixProvider>;

namespace ErrorProne.NET.StructAnalyzers.Tests
{
    [TestFixture]
    public class ExplicitInParameterTests
    {
        [Test]
        public async Task SimpleFix()
        {
            string code = @"
class Class {
    void Method(in int value) => throw null;
    void Caller(int value) => Method([|value|]);
}
";
            string expected = @"
class Class {
    void Method(in int value) => throw null;
    void Caller(int value) => Method(in value);
}
";

            await VerifyCS.VerifyCodeFixAsync(code, expected);
        }

        [Test]
        public async Task DefaultValue()
        {
            string code = @"
class Class {
    void Method(in int value) => throw null;
    void Caller() => Method(default);
}
";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task LiteralValue()
        {
            string code = @"
class Class {
    void Method(in int value) => throw null;
    void Caller() => Method(0);
}
";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task ConstantValue()
        {
            string code = @"
class Class {
    const int Value = 0;
    void Method(in int value) => throw null;
    void Caller() => Method(Value);
}
";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        [WorkItem(131, "https://github.com/SergeyTeplyakov/ErrorProne.NET/issues/131")]
        public async Task NoReferenceToThisClass()
        {
            string code = @"
class SomeClass {
  void Method(in SomeClass value) { }
  void Caller() { Method(this); }
}
";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        [WorkItem(131, "https://github.com/SergeyTeplyakov/ErrorProne.NET/issues/131")]
        public async Task ReferenceToThisStruct()
        {
            string code = @"
struct SomeStruct {
  void Method(in SomeStruct value) { }
  void Caller() { Method([|this|]); }
}
";
            string expected = @"
struct SomeStruct {
  void Method(in SomeStruct value) { }
  void Caller() { Method(in this); }
}
";

            await VerifyCS.VerifyCodeFixAsync(code, expected);
        }

        [Test]
        [WorkItem(132, "https://github.com/SergeyTeplyakov/ErrorProne.NET/issues/132")]
        public async Task NoReferenceToPropertyValue()
        {
            string code = @"
struct SomeStruct {
  SomeStruct Property => throw null;
  void Method(in SomeStruct value) { }
  void Caller() { Method(Property); }
}
";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        [WorkItem(132, "https://github.com/SergeyTeplyakov/ErrorProne.NET/issues/132")]
        public async Task ReferenceToRefPropertyValue()
        {
            string code = @"
struct SomeStruct {
  ref SomeStruct Property => throw null;
  void Method(in SomeStruct value) { }
  void Caller() { Method([|Property|]); }
}
";
            string expected = @"
struct SomeStruct {
  ref SomeStruct Property => throw null;
  void Method(in SomeStruct value) { }
  void Caller() { Method(in Property); }
}
";

            await VerifyCS.VerifyCodeFixAsync(code, expected);
        }

        [Test]
        [WorkItem(132, "https://github.com/SergeyTeplyakov/ErrorProne.NET/issues/132")]
        public async Task ReferenceToRefReadonlyPropertyValue()
        {
            string code = @"
struct SomeStruct {
  ref readonly SomeStruct Property => throw null;
  void Method(in SomeStruct value) { }
  void Caller() { Method([|Property|]); }
}
";
            string expected = @"
struct SomeStruct {
  ref readonly SomeStruct Property => throw null;
  void Method(in SomeStruct value) { }
  void Caller() { Method(in Property); }
}
";

            await VerifyCS.VerifyCodeFixAsync(code, expected);
        }

        [Test]
        [WorkItem(132, "https://github.com/SergeyTeplyakov/ErrorProne.NET/issues/132")]
        public async Task NoReferenceToMethodValue()
        {
            string code = @"
struct SomeStruct {
  SomeStruct GetProperty() => throw null;
  void Method(in SomeStruct value) { }
  void Caller() { Method(GetProperty()); }
}
";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        [WorkItem(132, "https://github.com/SergeyTeplyakov/ErrorProne.NET/issues/132")]
        public async Task ReferenceToRefMethodValue()
        {
            string code = @"
struct SomeStruct {
  ref SomeStruct GetProperty() => throw null;
  void Method(in SomeStruct value) { }
  void Caller() { Method([|GetProperty()|]); }
}
";
            string expected = @"
struct SomeStruct {
  ref SomeStruct GetProperty() => throw null;
  void Method(in SomeStruct value) { }
  void Caller() { Method(in GetProperty()); }
}
";

            await VerifyCS.VerifyCodeFixAsync(code, expected);
        }

        [Test]
        [WorkItem(132, "https://github.com/SergeyTeplyakov/ErrorProne.NET/issues/132")]
        public async Task ReferenceToRefReadonlyMethodValue()
        {
            string code = @"
struct SomeStruct {
  ref readonly SomeStruct GetProperty() => throw null;
  void Method(in SomeStruct value) { }
  void Caller() { Method([|GetProperty()|]); }
}
";
            string expected = @"
struct SomeStruct {
  ref readonly SomeStruct GetProperty() => throw null;
  void Method(in SomeStruct value) { }
  void Caller() { Method(in GetProperty()); }
}
";

            await VerifyCS.VerifyCodeFixAsync(code, expected);
        }

        [Test]
        [WorkItem(133, "https://github.com/SergeyTeplyakov/ErrorProne.NET/issues/133")]
        public async Task NonConstantDefaultLiteral()
        {
            string code = @"
struct MyStruct {
  MyStruct(string str) : this(default, str) { }
  MyStruct(in MyStruct value, string str) { }
}
";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        [WorkItem(133, "https://github.com/SergeyTeplyakov/ErrorProne.NET/issues/133")]
        public async Task NonConstantDefaultExpression()
        {
            string code = @"
struct MyStruct {
  MyStruct(string str) : this(default(MyStruct), str) { }
  MyStruct(in MyStruct value, string str) { }
}
";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }
    }
}

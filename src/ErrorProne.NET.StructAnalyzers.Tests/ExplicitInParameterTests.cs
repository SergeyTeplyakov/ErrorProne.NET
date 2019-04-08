﻿using NUnit.Framework;
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
    }
}

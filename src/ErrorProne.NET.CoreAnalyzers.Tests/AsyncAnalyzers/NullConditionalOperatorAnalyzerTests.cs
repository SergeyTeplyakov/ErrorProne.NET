﻿using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.NullConditionalOperatorAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class NullConditionalOperatorAnalyzerTests
    {
        [Test]
        public async Task Warn_For_Null_Conditional()
        {
            string code = @"
public class MyClass
{
    public async System.Threading.Tasks.Task Foo(MyClass m)
    {
       [|await m?.Foo(null)|];
    }
}
";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task No_Warn_When_Nullability_Is_Enabled()
        {
            // In this case the compiler will emit a warning.
            string code = @"
#nullable enable
public class MyClass
{
    public async System.Threading.Tasks.Task Foo(MyClass? m)
    {
       await m?.Foo(null);
    }
}
";
            await VerifyCS.VerifyAsync(code);
        }
    }
}
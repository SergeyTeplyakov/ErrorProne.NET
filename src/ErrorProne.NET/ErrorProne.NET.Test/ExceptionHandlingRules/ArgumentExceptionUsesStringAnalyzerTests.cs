using ErrorProne.NET.Common;
using ErrorProne.NET.Rules.ExceptionHandling;
using NUnit.Framework;
using RoslynNunitTestRunner;

namespace ErrorProne.NET.Test.ExceptionHandling
{
    [TestFixture]
    public class ArgumentExceptionUsesStringAnalyzerTests : CSharpAnalyzerTestFixture<ArgumentExceptionUsesStringAnalyzer> {
        [Test]
        public void ShouldWarnOnArgumentExceptionHasNoParametersInCtor() {
            var code = @"
using System;
class Test
{
    public Test(string arg) 
    {
        throw [|new ArgumentException()|];
    }
}";
            HasDiagnostic(code, RuleIds.ArgumentExceptionParamNameRequired);
        }

        [Test]
        public void ShouldWarnOnArgumentExceptionHasNoParametersInMethod() {
            var code = @"
using System;
class Test
{
    public Foo(string arg) 
    {
        throw [|new ArgumentException()|];
    }
}";
            HasDiagnostic(code, RuleIds.ArgumentExceptionParamNameRequired);
        }

        [Test]
        public void ShouldWarnOnArgumentExceptionHasOnlyMessageParameterInCtor() {
            var code = @"
using System;
class Test
{
    public Test(string arg) 
    {
        throw [|new ArgumentException(""It's an argument exception"")|];
    }
}";
            HasDiagnostic(code, RuleIds.ArgumentExceptionParamNameRequired);
        }

        [Test]
        public void ShouldWarnOnArgumentExceptionHasOnlyMessageParameterInMethod() {
            var code = @"
using System;
class Test
{
    public Foo(string arg) 
    {
        throw [|new ArgumentException(""It's an argument exception"")|];
    }
}";
            HasDiagnostic(code, RuleIds.ArgumentExceptionParamNameRequired);
        }

        [Test]
        public void ShouldWarnOnArgumentExceptionHasWrongParameterNameInCtor() {
            var code = @"
using System;
class Test
{
    public Test(string arg) 
    {
        throw new ArgumentException(""It's an argument exception"", [|""arg1""|]);
    }
}";
            HasDiagnostic(code, RuleIds.ArgumentExceptionMethodHasNoSuchParamName);
        }

        [Test]
        public void ShouldWarnOnArgumentExceptionHasWrongParameterNameInMethod() {
            var code = @"
using System;
class Test
{
    public Foo(string arg) 
    {
        throw new ArgumentException(""It's an argument exception"", [|""arg1""|]);
    }
}";
            HasDiagnostic(code, RuleIds.ArgumentExceptionMethodHasNoSuchParamName);
        }

        [Test]
        public void ShouldWarnOnArgumentExceptionHasStringParameterNameInCtor() {
            var code = @"
using System;
class Test
{
    public Test(string arg) 
    {
        throw new ArgumentException(""It's an argument exception"", [|""arg""|]);
    }
}";
            HasDiagnostic(code, RuleIds.ArgumentExceptionParamNameShouldNotBeString);
        }

        [Test]
        public void ShouldWarnOnArgumentExceptionHasStringParameterNameInMethod() {
            var code = @"
using System;
class Test
{
    public Foo(string arg) 
    {
        throw new ArgumentException(""It's an argument exception"", [|""arg""|]);
    }
}";
            HasDiagnostic(code, RuleIds.ArgumentExceptionParamNameShouldNotBeString);
        }

        [Test]
        public void ShouldNotWarnOnArgumentExceptionHasNameofInCtor() {
            var code = @"
using System;
class Test
{
    public Test(string arg) 
    {
        throw new ArgumentException(""It's an argument exception"", nameof(arg));
    }
}";
            NoDiagnostic(code, RuleIds.ArgumentExceptionParamNameRequired);
            NoDiagnostic(code, RuleIds.ArgumentExceptionMethodHasNoSuchParamName);
            NoDiagnostic(code, RuleIds.ArgumentExceptionParamNameShouldNotBeString);
        }

        [Test]
        public void ShouldNotWarnOnArgumentExceptionHasNameofInMethod() {
            var code = @"
using System;
class Test
{
    public Foo(string arg) 
    {
        throw new ArgumentException(""It's an argument exception"", nameof(arg));
    }
}";
            NoDiagnostic(code, RuleIds.ArgumentExceptionParamNameRequired);
            NoDiagnostic(code, RuleIds.ArgumentExceptionMethodHasNoSuchParamName);
            NoDiagnostic(code, RuleIds.ArgumentExceptionParamNameShouldNotBeString);
        }
    }
}

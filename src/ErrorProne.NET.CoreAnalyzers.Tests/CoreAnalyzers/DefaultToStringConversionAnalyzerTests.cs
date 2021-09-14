using NUnit.Framework;
using System.Threading.Tasks;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.DefaultToStringImplementationUsageAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class DefaultToStringConversionAnalyzerTests
    {
        [Test]
        public async Task InterpolatedStringConversionWithMethodCall()
        {
            var test = @"
public class Task {}
class Test {
    Task FooBar() => null;
    string T() => $""fb: {[|FooBar()|]}"";
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task InterpolatedStringConversionWithMethodCallForTuple()
        {
            var test = @"
public class Task {}
class Test {
    (Task t1, int x) FooBar() => default;
    string T() => $""fb: {[|FooBar()|]}"";
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task InterpolatedStringConversionWithMethodCallForStruct()
        {
            var test = @"
public struct Task {}
class Test {
    Task FooBar() => default;
    string T() => $""fb: {[|FooBar()|]}"";
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task InterpolatedStringConversionWithMethodCallAndLocal()
        {
            var test = @"
public class Task {}
class Test {
    Task FooBar() => null;
    string T()
    {
        var fb = FooBar();
        return $""fb: {[|fb|]}"";
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task InterpolatedStringConversionWithVariable()
        {
            var test = @"
public class Task {}
class Test {
    string T(Task t) => $""t: {[|t|]}"";
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task InterpolatedStringConversionInStringConcat()
        {
            var test = @"
public class Task {}
class Test {
    string T(Task t) => $""t"" + [|t|];
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task InterpolatedStringConversionInStringConcat2()
        {
            var test = @"
public class Task {}
class Test {
    void T(Task t) => Foo($""t"" + [|t|]);
    void Foo(string s) {}
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task InterpolatedStringConversionReturn()
        {
            var test = @"
public class Task {}
class Test {
    string T(Task t) => string.Format(""{0}"", [|t|]);
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task InterpolatedStringConversionReturnExplicit()
        {
            var test = @"
public class Task {}
class Test {
    string T(Task t) => string.Format(""{0}"", [|t.ToString()|]);
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task StringBuilderAppend()
        {
            var test = @"
public class Task {}
class Test {
    string T(Task t) => new System.Text.StringBuilder().Append([|t|]).ToString();
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarnOnConversionToObject()
        {
            var test = @"
public class Task {}
class Test {
    object T(Task t) => t;
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarnOnConversionToObjectMethodCall()
        {
            var test = @"
public class Task {}
class Test {
    void T(Task t) => Foo(t);
    void Foo(object o) {}
}
";
            await Verify.VerifyAsync(test);
        }
    }
}
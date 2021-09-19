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
        public async Task NoWarnOnEnum()
        {
            var test = @"
enum Task {}
class Test {
    public static string Foo(Task tsk) => tsk.ToString();
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarnOnLast()
        {
            var test = @"
using System.Linq;
class Test {
    private readonly string _s;
    public Test(string[] s)
    {
        _s = s.Last();
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarnOnEquality()
        {
            var test = @"
public class Task {}
class Test {
    public string Foo(Task tsk) => tsk == null ? 1.ToString() : 2.ToString();
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarnOnCastFromObject()
        {
            var test = @"
using System.Linq;
class Test {
    private readonly string _s;
    public Test(object s)
    {
        _s = (string)s;
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarnOnInterfaceOrAbstractClass()
        {
            var test = @"
interface IFoo {}
abstract class Foo {}
class Test {
    private readonly string _s;
    public Test(IFoo foo, Foo foo2)
    {
        _s = foo.ToString();
        _s = foo2.ToString();
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NoWarnOnGenerics()
        {
            var test = @"
class Test<T> {
    private readonly string _s;
    public Test(T foo)
    {
        _s = foo.ToString();
    }

    public string Foo<U>(U foo)
    {
        return foo.ToString();
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task NullFieldAssignment()
        {
            var test = @"
class Test {
    private readonly string _s;
    public Test()
    {
        _s = null;
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
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
        public async Task StringFormatConversion()
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
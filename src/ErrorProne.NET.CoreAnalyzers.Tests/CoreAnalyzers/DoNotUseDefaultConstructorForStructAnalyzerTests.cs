using ErrorProne.NET.TestHelpers;
using NUnit.Framework;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.DoNotUseDefaultConstructorForStructAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class DoNotUseDefaultConstructorForStructAnalyzerTests
    {
        [Test]
        public async Task Warn_On_Explicit_Construction()
        {
            string code = @"
[DoNotUseDefaultConstructionAttribute]
public struct MyS
{
    public static void Check()
    {
        var s = [|new MyS()|];
        var a = new MyS[1]; // ok
        a[0] = [|new MyS()|];
        var lst = new System.Collections.Generic.List<MyS>();
        lst.Add([|new MyS()|]);
        Process([|new MyS()|]);
    }
    public static void Process<T>(T value) {}
}";

            await VerifySource(code);
        }

        [Test]
        public async Task Warn_On_Default_Construction()
        {
            string code = @"
[DoNotUseDefaultConstructionAttribute]
public struct MyS
{
}
public class Foo
{
    private MyS _s = [|default|];
    public void Bar(MyS s = [|default|]) {}
    public void Check()
    {
        MyS s = [|default|];
        MyS s2 = [|default(MyS)|];
    }
}
";

            await VerifySource(code);
        }

        public struct MyS
        {
        }

        public struct S2
        {
            private MyS _s;
        }
        // generics.
        public static T Create<T>() where T : new()
        {
            return default;
        }
        [Test]
        public async Task Warn_If_Embedded_In_Another_Struct_Not_Marked_With_The_Same_Attribute()
        {
            string code = @"
[DoNotUseDefaultConstructionAttribute]
public struct MyS
{
}

public struct S2
{
  private [|MyS _s|];
}

[DoNotUseDefaultConstructionAttribute]
public struct S3
{
  private MyS _s;
}
";

            await VerifySource(code);
        }

        private static async Task VerifySource(string code)
        {
            await new VerifyCS.Test
                {
                    TestState =
                    {
                        Sources = {code},
                    },
                }
                .WithDoNotUseDefaultConstructionAttribute()
                .WithoutGeneratedCodeVerification()
                .RunAsync();
        }
    }
}
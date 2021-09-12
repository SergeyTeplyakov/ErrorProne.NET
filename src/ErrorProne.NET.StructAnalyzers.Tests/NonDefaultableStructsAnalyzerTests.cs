using ErrorProne.NET.TestHelpers;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.Net.StructAnalyzers.NonDefaultStructs.NonDefaultableStructsCreationAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

using VerifyEmbedCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.Net.StructAnalyzers.NonDefaultStructs.NonDefaultableStructsFieldAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class NonDefaultableStructsAnalyzerTests
    {
        // Missing cases:
        // (MyNonDefaultableStruct ms, int x) tpl = default;
        // var a = new MyNonDefaultableStruct[42]; // Should we warn here?

        [Test]
        public async Task Warn_When_Struct_Mutates_Itself()
        {
            string code = @"
[NonDefaultable]
    public struct NonReadonlyStruct
    {
        private int x;

        public NonReadonlyStruct(int x)
        {
            this.x = x;
        }

        public void Mutate()
        {
            this = [|default|];
        }
    }
";

            await VerifySource(code);
        }
        
        [Test]
        public async Task No_Warn_When_Struct_Is_Embedded_Into_Class()
        {
            string code = @"
[NonDefaultableAttribute]
public struct MyS { }
public class MyC {private readonly MyS _s;}
";

            await VerifyEmbeddedStruct(code);
        }
        
        [Test]
        public async Task Warn_On_Explicit_Construction()
        {
            string code = @"
[NonDefaultableAttribute]
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
[NonDefaultableAttribute]
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

        [Test]
        public async Task Warn_On_Default_Construction_Via_Factory()
        {
            string code = @"
[NonDefaultableAttribute]
public struct MyS
{
}
public class Foo
{
    public void Check()
    {
        MyS s = [|Create<MyS>()|];
        MyS s2 = [|Create2<int, MyS>(42)|];
    }

    public static T Create<T>() where T: new() => new T();
    public static U Create2<T, U>(T t) where U: new() => new U();
}
";

            await VerifySource(code);
        }
        
        [Test]
        public async Task Warn_On_Default_Construction_Via_Out_Or_Ref_Generic()
        {
            string code = @"
[NonDefaultableAttribute]
public struct MyS
{
  public MyS(int n) {}
}
public class Foo
{
    public void Check()
    {
        [|CreateOutput<MyS>(out var s)|];

        [|CreateRef(ref s)|];
    }

    // Should warn even without any constraints.
    public static void CreateOutput<T>(out T t)
    {
        t = default;
    }
    
    public static void CreateRef<T>(ref T t)
    {
        t = default;
    }
}
";

            await VerifySource(code);
        }
        
        [Test]
        public async Task Warn_On_Default_Construction_Via_Out_Or_Non_Generic()
        {
            string code = @"
[NonDefaultableAttribute]
public struct MyS
{
  public MyS(int n) {}
}
public class Foo
{
    public static void CreateOutput(out MyS t)
    {
        t = [|default|];
    }
    
    public static void CreateRef(ref MyS t)
    {
        t = [|new MyS()|];
    }
}
";

            await VerifySource(code);
        }
        
        [Test]
        public async Task Warn_On_Default_Construction_And_Emit_Provided_Error_Message()
        {
            string code = @"
[NonDefaultableAttribute(""Use MyS.Create() instead"")]
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

        [Test]
        public async Task Warn_If_Embedded_As_Field()
        {
            string code = @"
[NonDefaultableAttribute]
public struct MyS
{
}

public struct S2
{
  [|private MyS _s;|]
}
";

            await VerifyEmbeddedStruct(code);
        }
        
        [Test]
        public async Task Warn_If_Embedded_As_Auto_Get_Only_Property()
        {
            string code = @"
[NonDefaultableAttribute]
public struct MyS
{
}

public struct S2
{
  [|private MyS _s {get;}|]
}
";

            await VerifyEmbeddedStruct(code);
        }
        
        [Test]
        public async Task Warn_If_Embedded_As_Auto_Property()
        {
            string code = @"
[NonDefaultableAttribute]
public struct MyS
{
}

public struct S2
{
  [|private MyS _s2 {get; set;}|]
}
";

            await VerifyEmbeddedStruct(code);
        }
        
        [Test]
        public async Task No_Warn_For_Computed_Property()
        {
            string code = @"
[NonDefaultableAttribute]
public struct MyS
{
}

public struct S2
{
  private MyS P1 => default;
  private MyS P2 { get => default;}
}
";

            await VerifyEmbeddedStruct(code);
        }
        
        [Test]
        public async Task No_Warn_If_Embedded_As_Field_Into_Struct_Marked_With_The_Same_Attribute()
        {
            string code = @"
[NonDefaultableAttribute]
public struct MyS
{
}

[NonDefaultableAttribute]
public struct S2
{
  private MyS _s;
}
";

            await VerifyEmbeddedStruct(code);
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
                .WithNonDefaultableAttribute()
                .WithoutGeneratedCodeVerification()
                .RunAsync();
        }
        
        private static async Task VerifyEmbeddedStruct(string code)
        {
            await new VerifyEmbedCS.Test
                {
                    TestState =
                    {
                        Sources = {code},
                    },
                }
                .WithNonDefaultableAttribute()
                .WithoutGeneratedCodeVerification()
                .RunAsync();
        }
    }
}
using ErrorProne.NET.TestHelpers;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.NonDefaultStructs.DoNotCreateStructWithNoDefaultStructConstructionAttributeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

using VerifyEmbedCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.NonDefaultStructs.DoNotEmbedStructsWithNoDefaultStructConstructionAttributeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class DefaultStructConstructionAnalyzerTests
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
        
        [Test]
        public async Task Warn_On_Default_Construction_And_Emit_Provided_Error_Message()
        {
            string code = @"
[DoNotUseDefaultConstructionAttribute(""Use MyS.Create() instead"")]
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
#pragma warning disable CS0169 // The field 'field_name' is never used
            private MyS _s;
#pragma warning restore CS0169 // The field 'field_name' is never used
        }
        // generics.
        public static T Create<T>() where T : new()
        {
#pragma warning disable CS8603 // Possible null reference return.
            return default;
#pragma warning restore CS8603 // Possible null reference return.
        }
        
        [Test]
        public async Task Warn_If_Embedded_As_Field()
        {
            string code = @"
[DoNotUseDefaultConstructionAttribute]
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
[DoNotUseDefaultConstructionAttribute]
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
[DoNotUseDefaultConstructionAttribute]
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
[DoNotUseDefaultConstructionAttribute]
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
[DoNotUseDefaultConstructionAttribute]
public struct MyS
{
}

[DoNotUseDefaultConstructionAttribute]
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
                .WithDoNotUseDefaultConstructionAttribute()
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
                .WithDoNotUseDefaultConstructionAttribute()
                .WithoutGeneratedCodeVerification()
                .RunAsync();
        }
    }
}
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
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

        [Test]
        public async Task NoWarn_When_Type_Has_Implicit_Conversion_To_String()
        {
            // Issue #317: a type that defines 'public static implicit operator string'
            // opts into its own string representation, so EPC20 should not fire.
            var test = @"
public class AmazonFile
{
    public static implicit operator string(AmazonFile f) => ""x"";
}
class Test {
    string Concat(AmazonFile f) => ""x"" + f;
    string Interp(AmazonFile f) => $""{f}"";
    string Format(AmazonFile f) => string.Format(""{0}"", f);
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task Warn_When_Implicit_Conversion_Returns_Something_Other_Than_String()
        {
            var test = @"
public class AmazonFile
{
    public static implicit operator int(AmazonFile f) => 0;
}
class Test {
    string Concat(AmazonFile f) => ""x"" + [|f|];
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_In_Generated_Code()
        {
            // Issue #337: generated code (e.g. source generator output) is not under the user's control,
            // so EPC20 should not fire there.
            var test = @"
// <auto-generated/>
public class Task {}
class Test {
    string T(Task t) => $""t: {t}"";
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task Warn_Only_In_Authored_Part_Of_Partially_Generated_Type()
        {
            // Issue #337: a source generator (e.g. Thinktecture smart enums) can emit a partial type
            // whose generated part uses the default ToString. Only the authored part should be flagged.
            await new Verify.Test
            {
                LanguageVersion = LanguageVersion.Latest,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                TestState =
                {
                    Sources =
                    {
                        ("Status.cs", @"
public partial class Status
{
    public string Describe() => $""Status: {[|this|]}"";
}
"),
                        ("Status.SmartEnum.g.cs", @"// <auto-generated />
public partial class Status
{
    private void ThrowUnknownItem()
    {
        throw new System.InvalidOperationException($""Unknown item '{this}'."");
    }
}
"),
                    },
                },
            }.RunAsync();
        }

        [Test]
        public async Task Warn_In_Authored_Partial_Method_With_Generated_Declaration()
        {
            await new Verify.Test
            {
                LanguageVersion = LanguageVersion.Latest,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                TestBehaviors = TestBehaviors.SkipGeneratedCodeCheck,
                TestState =
                {
                    Sources =
                    {
                        ("ViewModel.cs", @"
class Item {}
[System.CodeDom.Compiler.GeneratedCode(""Generator"", ""1.0"")]
class OtherGenerated
{
    string Render(Item value) => $""{value}"";
}
partial class ViewModel
{
    partial void OnChanged(Item value) => System.Console.WriteLine($""{[|value|]}"");
}
"),
                        ("ViewModel.g.cs", @"// <auto-generated />
partial class ViewModel
{
    [System.CodeDom.Compiler.GeneratedCode(""Generator"", ""1.0"")]
    partial void OnChanged(Item value);
}
"),
                    },
                },
            }.RunAsync();
        }

        [Test]
        public async Task Warn_In_Authored_Partial_Method_With_GeneratedCode_Declaration_In_Plain_File()
        {
            await new Verify.Test
            {
                LanguageVersion = LanguageVersion.Latest,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                TestBehaviors = TestBehaviors.SkipGeneratedCodeCheck,
                TestState =
                {
                    Sources =
                    {
                        ("ViewModel.cs", @"
class Item {}
partial class ViewModel
{
    partial void OnChanged(Item value) => System.Console.WriteLine($""{[|value|]}"");
}
"),
                        ("ViewModel.Declarations.cs", @"
partial class ViewModel
{
    [System.CodeDom.Compiler.GeneratedCode(""Generator"", ""1.0"")]
    partial void OnChanged(Item value);
}
"),
                    },
                },
            }.RunAsync();
        }

        [Test]
        public async Task Warn_Only_In_Authored_Partial_Implementation_In_Same_File()
        {
            await Verify.VerifyAsync(@"
class Item {}
partial class ViewModel
{
    [System.CodeDom.Compiler.GeneratedCode(""Generator"", ""1.0"")]
    partial void OnChanged(Item value);
    partial void OnChanged(Item value) => System.Console.WriteLine($""{[|value|]}"");

    partial void OnGenerated(Item value);
    [System.CodeDom.Compiler.GeneratedCode(""Generator"", ""1.0"")]
    partial void OnGenerated(Item value) => System.Console.WriteLine($""{value}"");
}
");
        }

        [TestCase("Component.razor")]
        [TestCase("View.cshtml")]
        public async Task Warn_In_Razor_Mapped_Code_But_Not_Generated_Code(string mappedPath)
        {
            var source = @"
// <auto-generated />
class Item {}
class Component
{
#line 1 """ + mappedPath + @"""
    string Authored(Item value) => $""{[|value|]}"";
#line hidden
    string Hidden(Item value) => $""{value}"";
#line default
    string Generated(Item value) => $""{value}"";
}
";
            await new Verify.Test
            {
                LanguageVersion = LanguageVersion.Latest,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                TestBehaviors = TestBehaviors.SkipGeneratedCodeCheck,
                TestState = { Sources = { ("Component.g.cs", source) } },
            }.RunAsync();
        }

        [TestCase("Component.g.cs")]
        [TestCase("Component.g.i.cs")]
        [TestCase("Component.generated.cs")]
        [TestCase("Component.designer.cs")]
        [TestCase("TemporaryGeneratedFile_Component.cs")]
        public async Task NoWarn_In_Generated_File_Without_Header(string fileName)
        {
            await new Verify.Test
            {
                LanguageVersion = LanguageVersion.Latest,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                TestBehaviors = TestBehaviors.SkipGeneratedCodeCheck,
                TestState =
                {
                    Sources = { (fileName, @"
class Item {}
class Component
{
    string Generated(Item value) => $""{value}"";
}
") },
                },
            }.RunAsync();
        }

        [Test]
        public async Task NoWarn_In_Method_Marked_As_Generated()
        {
            await Verify.VerifyAsync(@"
class Item {}
class Component
{
    [System.CodeDom.Compiler.GeneratedCode(""Generator"", ""1.0"")]
    string Generated(Item value) => $""{value}"";
}
");
        }

        [Test]
        public async Task Warn_Only_In_Authored_Property_Accessors()
        {
            await Verify.VerifyAsync(@"
class Item {}
class Component
{
    private Item item = new Item();

    [System.CodeDom.Compiler.GeneratedCode(""Generator"", ""1.0"")]
    public string Generated
    {
        get { return $""{item}""; }
        set { System.Console.WriteLine($""{item}""); }
    }

    public string Authored
    {
        get { return $""{[|item|]}""; }
        set { System.Console.WriteLine($""{[|item|]}""); }
    }
}
");
        }

        [Test]
        public async Task Warn_Only_In_Authored_Event_Accessors()
        {
            await Verify.VerifyAsync(@"
class Item {}
class Component
{
    private Item item = new Item();

    [System.CodeDom.Compiler.GeneratedCode(""Generator"", ""1.0"")]
    public event System.Action Generated
    {
        add { System.Console.WriteLine($""{item}""); }
        remove { System.Console.WriteLine($""{item}""); }
    }

    public event System.Action Authored
    {
        add { System.Console.WriteLine($""{[|item|]}""); }
        remove { System.Console.WriteLine($""{[|item|]}""); }
    }
}
");
        }

        [Test]
        public async Task NoWarn_In_Partial_Implementation_Explicitly_Marked_As_Generated()
        {
            await new Verify.Test
            {
                LanguageVersion = LanguageVersion.Latest,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                TestBehaviors = TestBehaviors.SkipGeneratedCodeCheck,
                TestState =
                {
                    Sources =
                    {
                        ("ViewModel.cs", @"
class Item {}
partial class ViewModel
{
    [System.CodeDom.Compiler.GeneratedCode(""Generator"", ""1.0"")]
    partial void OnChanged(Item value) => System.Console.WriteLine($""{value}"");
}
"),
                        ("ViewModel.g.cs", @"// <auto-generated />
partial class ViewModel
{
    partial void OnChanged(Item value);
}
"),
                    },
                },
            }.RunAsync();
        }

        [Test]
        public async Task Warn_In_File_Explicitly_Marked_As_Not_Generated()
        {
            await new Verify.Test
            {
                LanguageVersion = LanguageVersion.Latest,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                TestBehaviors = TestBehaviors.SkipGeneratedCodeCheck,
                TestState =
                {
                    Sources = { ("/Component.g.cs", @"// <auto-generated />
class Item {}
class Component
{
    string Render(Item value) => $""{[|value|]}"";
}
") },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true
[*.g.cs]
generated_code = false
") },
                },
            }.RunAsync();
        }

        [Test]
        public async Task NoWarn_In_File_Explicitly_Marked_As_Generated()
        {
            await new Verify.Test
            {
                LanguageVersion = LanguageVersion.Latest,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                TestBehaviors = TestBehaviors.SkipGeneratedCodeCheck,
                TestState =
                {
                    Sources = { ("/Component.cs", @"
class Item {}
class Component
{
#line 1 ""Component.razor""
    string Render(Item value) => $""{value}"";
}
") },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true
[*.cs]
generated_code = true
") },
                },
            }.RunAsync();
        }

        [Test]
        public async Task NoWarn_In_Generated_Code_Mapped_To_CSharp()
        {
            await new Verify.Test
            {
                LanguageVersion = LanguageVersion.Latest,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                TestBehaviors = TestBehaviors.SkipGeneratedCodeCheck,
                TestState =
                {
                    Sources = { ("Component.g.cs", @"
class Item {}
class Component
{
#line 1 ""Input.cs""
    string Render(Item value) => $""{value}"";
}
") },
                },
            }.RunAsync();
        }
    }
}
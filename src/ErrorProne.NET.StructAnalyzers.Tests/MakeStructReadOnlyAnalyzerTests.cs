using ErrorProne.NET.TestHelpers;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.StructAnalyzers.MakeStructReadOnlyAnalyzer,
    ErrorProne.NET.StructAnalyzers.MakeStructReadOnlyCodeFixProvider>;

namespace ErrorProne.NET.StructAnalyzers.Tests
{
    [TestFixture]
    public class MakeStructReadOnlyAnalyzerTests
    {
        [Test]
        public async Task ThisAssignmentShouldPreventTheWarning()
        {
            string code = @"struct SelfAssign {
    public readonly int Field;

    public void M(SelfAssign other) {
if (other.Field > 0)      
this = other;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);

            string code2 = @"struct SelfAssign {
    public readonly int Field;

    public void M(SelfAssign other) => 
      this = other;
    
}";

            await VerifyCS.VerifyAnalyzerAsync(code2);

            string code3 = @"struct SelfAssign
        {
            public readonly int Field;
            public SelfAssign(int f) => Field = f;

            public void Foo()
            {
                ref SelfAssign x = ref this;
                x = new SelfAssign(42);
            }
        }";
            await VerifyCS.VerifyAnalyzerAsync(code3);
        }

        [Test]
        public async Task ThisAssignmentInPropertyShouldPreventTheWarning()
        {
            string code = @"struct SelfAssign2
        {
            public int X
            {
                get
                {
                    this = new SelfAssign2();
                    return 32;
                }
            }
        }";

            await VerifyCS.VerifyAnalyzerAsync(code);

        //    string code2 = @"struct SelfAssign2
        //{
        //    public int X
        //    {
        //        set
        //        {
        //            this = new SelfAssign2();
        //        }
        //    }
        //}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task HasDiagnosticSelfAssignmentInConstructor()
        {
            string code = @"struct [|SelfAssign2|]
        {
            private readonly int _x;
            public SelfAssign2(int x)
            {
                _x = x;
                this = new SelfAssign2();
            }
        }";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task HasDiagnosticForRefReadonly()
        {
            string code = @"struct [|SelfAssign|]
        {
            public readonly int Field;
            public SelfAssign(int f) => Field = f;

            public void Foo()
            {
                ref readonly SelfAssign x = ref this;
            }
        }";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task HasDiagnosticForIn()
        {
            string code = @"struct [|SelfAssign|]
        {
            public readonly int Field;
            public SelfAssign(int f) => Field = f;

            public void Foo()
            {
                Bar(in this);
            }
            public void Bar(in SelfAssign sa) {}
        }";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task HasDiagnosticsForEmptyStruct()
        {
            string code = @"struct [|FooBar|] {}";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        [TestCaseSource(nameof(GetHasDiagnosticCases))]
        public async Task HasDiagnosticCases(string code)
        {
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
            }.WithoutGeneratedCodeVerification().RunAsync();
        }

        public static IEnumerable<string> GetHasDiagnosticCases()
        {
            // With constructor only
            yield return 
@"struct [|FooBar|] {
    public FooBar(int n):this() {}
}";

            // With one private field
            yield return
@"struct [|FooBar|] {
    private readonly int _x;
    public FooBar(int x) => _x = x;
}";
            
            // With one public readonly property
            yield return
@"struct [|FooBar|] {
    public int X {get;}
    public FooBar(int x) => X = x;
}";
            
            // With one public get-only property
            yield return
@"struct [|FooBar|] {
    public int X => 42;
}";
            
            // With one public readonly property and method
            yield return
@"struct [|FooBar|] {
    public int X {get;}
    public FooBar(int x) => X = x;
    public int GetX() => X;
}";
            
            // With const
            yield return
@"struct [|FooBar|] {
    private readonly int _x;
    private const int MaxLength = 1;
}";
            // With indexer
            yield return
@"struct [|FooBar|]<T> {
    private const int Index = 0;
    private readonly T[] _buffer;
    public T Value
        {
            get
            {
                return _buffer[Index];
            }

            set
            {
                _buffer[Index] = value;
            }
        }
}";
            
            // With getter and setter
            yield return
@"struct [|FooBar|] {
    public int Value
        {
            get
            {
                return 42;
            }

            set
            {
                
            }
        }
}";
            
            // With getter and setter as expression-bodies
            yield return
@"struct [|FooBar|] {
  private readonly object[] _data;  
  public int Value
        {
            get => 42;

            set => _data[0] = value;
        }
}";

        }

        [Test]
        public async Task NoDiagnosticCasesWhenStructIsAlreadyReadonly()
        {
            string code = @"readonly struct FooBar {}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task NoDiagnosticCasesWhenStructIsAlreadyReadonlyWithPartialDeclaation()
        {
            string code = @"partial struct FooBar {} readonly partial struct FooBar {}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [TestCaseSource(nameof(GetNoDiagnosticCases))]
        public async Task NoDiagnosticCases(string code)
        {
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        public static IEnumerable<string> GetNoDiagnosticCases()
        {
            // Enums should not be readonly
            yield return @"enum FooBar {}";

            // Already marked with 
            yield return 
@"readonly struct FooBar {
    public FooBar(int n):this() {}
}";

            // Non-readonly field
            yield return
@"struct FooBar {
    private int _x;
}";
            
            // With a setter
            yield return
@"struct FooBar {
    public int X {get; private set;}
}";

        }
    }
}
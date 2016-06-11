using System;
using System.Collections.Generic;
using System.Linq;
using ErrorProne.NET.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;

namespace ErrorProne.NET.Test.UtilTests
{
    [TestFixture]
    public unsafe class StructSizeCalculatorTests
    {
        [TestCaseSource(nameof(TestCases))]
        public int TestComputeSize(string code)
        {
            SemanticModel model;
            var typeSymbol = GetTypeFor(code, out model);

            var result = typeSymbol.ComputeStructSize(model);

            Console.WriteLine($"Resulting size is: {result}");
            return result;
        }

        private static IEnumerable<TestCaseData> TestCases()
        {
            return new[]
            {
                new TestCaseData("struct EmptyStruct { }").Returns(sizeof(EmptyStruct)),
                new TestCaseData("struct WithEnum {enum Foo : byte { Val1 } Foo f;}").Returns(sizeof(WithByteEnum)),
                new TestCaseData("struct WithSystemKindEnum { System.DateTimeKind f; }").Returns(sizeof(WithSystemKindEnum)),
                new TestCaseData("struct DecimalAndByte { decimal d; byte b; }").Returns(sizeof(DecimalAndByte)),
                new TestCaseData("struct S {int n;}").Returns(sizeof(S1)),
                new TestCaseData("struct S {byte b1; byte b2;}").Returns(sizeof(ByteByte)),
                new TestCaseData("struct S {byte b1; byte b2; byte b3;}").Returns(sizeof(ThreeBytes)),
                new TestCaseData("struct StructWith3BytesAndByte { struct N { byte b1; byte b2; byte b3; } N n; byte b; byte b2; }").Returns(sizeof(StructWith3BytesAnd2Byte)),
                new TestCaseData("struct S {byte b1; long l;}").Returns(sizeof(ByteLong)),
                new TestCaseData("struct S {byte b1; byte b2; long l;}").Returns(sizeof(ByteByteLong)),
                new TestCaseData("struct IntByteByteLong { int n; byte b1; byte b2; long l; }").Returns(sizeof(IntByteByteLong)),
                new TestCaseData("struct Int5BytesLong { int i;  byte b1; byte b2; byte b3; byte b4; byte b5; long l;}").Returns(sizeof(Int5BytesLong)),
                new TestCaseData("struct IntByteByteLongIntByteByteLong { int n; byte b1; byte b2; long l; int n2; byte b3; byte b4; long l2; }").Returns(sizeof(IntByteByteLongIntByteByteLong)),
                new TestCaseData("struct LongByteByteInt { long l; byte b1; byte b2; int n; }").Returns(sizeof(LongByteByteInt)),
                new TestCaseData("struct S {byte b1; long l; byte b2;}").Returns(sizeof(ByteLongByte)),
                new TestCaseData("struct S {byte n;}").Returns(sizeof(SingleByte)),
                new TestCaseData("struct StructWith9Bytes { byte b1; byte b2; byte b3; byte b4; byte b5; byte b6; byte b7; byte b8; byte b9; }").Returns(sizeof(StructWith9Bytes)),
                new TestCaseData("struct StructWith9BytesNested { struct N { byte b1; byte b2; byte b3; byte b4; byte b5; byte b6; byte b7; byte b8; byte b9; } N n; int i;  }")
                .Returns(sizeof(StructWith9BytesNested)),
                new TestCaseData("struct S {bool n;}").Returns(sizeof(SingleBool)),
                new TestCaseData("struct S {byte? n;}").Returns(sizeof(FakedNullableByte)),
                new TestCaseData("struct S {byte n; short s;}").Returns(sizeof(ByteShort)),
                new TestCaseData("struct S {int n; int n2;}").Returns(sizeof(IntInt)),
                new TestCaseData("struct S {struct N {int n;} N n; int i;}").Returns(sizeof(S2)),
                new TestCaseData("struct S {struct N {byte n;} N n; int i;}").Returns(sizeof(NestedStructByteAndInt)),
                new TestCaseData("struct NestedWithLongAndByteAndInt { struct N { byte b; long l; } N n; int i; }").Returns(sizeof(NestedWithLongAndByteAndInt)),
                new TestCaseData("struct NestedWithLongAndByteAndInt2 { struct N { byte b; long l; } N n; N n2; int i; }").Returns(sizeof(NestedWithLongAndByteAndInt2)),
                new TestCaseData("struct NestedWithByteLongByteAndInt { struct N { byte b; long l; byte b2; } N n; int i; }").Returns(sizeof(NestedWithByteLongByteAndInt)),
                new TestCaseData("struct S {byte b; int n;}").Returns(sizeof(ByteInt)),
                new TestCaseData("struct S {int n; byte b;}").Returns(sizeof(IntByte)),
                new TestCaseData("struct S {byte b; byte b2; int n;}").Returns(sizeof(S4)),
                new TestCaseData("struct S {byte b; byte b2; int n; byte b3;}").Returns(sizeof(ByteByteIntByte)),
                new TestCaseData("struct S {long l; int n;}").Returns(sizeof(S5)),
                new TestCaseData("struct S {long l; int n; object o;}").Returns(sizeof(S5) + IntPtr.Size),
            };
        }
#pragma warning disable CS0169
        struct StructWith9Bytes { byte b1; byte b2; byte b3; byte b4; byte b5; byte b6; byte b7; byte b8; byte b9; }
        struct StructWith9BytesNested { struct N { byte b1; byte b2; byte b3; byte b4; byte b5; byte b6; byte b7; byte b8; byte b9; } N n; int i;  }

        struct StructWith3BytesAnd2Byte { struct N { byte b1; byte b2; byte b3; } N n; byte b; byte b2; }

        struct DecimalAndByte { decimal d; byte b; } // size 20
        struct S1 { int n; }
        struct EmptyStruct { }
        struct WithByteEnum { enum Foo : byte { } Foo f; }
        struct WithSystemKindEnum { System.PlatformID f; }
        struct SingleByte { byte n; }
        struct ThreeBytes { byte b1; byte b2; byte b3; }
        struct SingleBool { bool b; }
        struct FakedNullableByte { bool b; byte n; }

        struct ByteByte { byte b1; byte b2; }
        struct Int5BytesLong { int i;  byte b1; byte b2; byte b3; byte b4; byte b5; long l;}
        struct ByteLong { byte b1; long l; }
        struct ByteLongByte { byte b1; long l; byte b2; }
        struct ByteByteLong { byte b1; byte b2; long l; }
        struct ByteShort { byte n; short s; }
        struct IntInt { int n; int n2; }
        struct IntByteByteLong { int n; byte b1; byte b2; long l; }
        struct IntByteByteLongIntByteByteLong { int n; byte b1; byte b2; long l; int n2; byte b3; byte b4; long l2; }
        struct NestedWithByteLongByteAndInt { struct N { byte b; long l; byte b2; } N n; int i; }
        struct LongByteByteInt { long l; byte b1; byte b2; int n; }

        struct S2 { struct N { int n; } N n; int i; }
        struct NestedStructByteAndInt { struct N { byte b; } N n; int i; }
        struct NestedWithLongAndByteAndInt { struct N { byte b; long l; } N n; int i; }
        struct NestedWithLongAndByteAndInt2 { struct N { byte b; long l; } N n1; N n2; int i; }

        struct ByteInt { byte b; int n; }
        struct IntByte { int n; byte b; }

        struct S4 { byte b; byte b2; int n; }
        struct ByteByteIntByte { byte b; byte b2; int n; byte b3; }
        struct S5 { long l; int n; }
        struct S6 { long l; int n; object o; }
#pragma warning restore CS0169

        [Test]
        public void TestComputeOnStruct()
        {
            string code = @"
struct S {
  private int n;
}";

            SemanticModel semanticModel;
            var typeSymbol = GetTypeFor(code, out semanticModel);
            var size = typeSymbol.ComputeStructSize(semanticModel);

            Assert.That(size, Is.EqualTo(sizeof(int)));
        }

        private ITypeSymbol GetTypeFor(string code, out SemanticModel semanticModel)
        {
            var tree = CSharpSyntaxTree.ParseText(code);

            var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            var compilation = CSharpCompilation.Create("MyCompilation",
                syntaxTrees: new[] { tree }, references: new[] { mscorlib });

            semanticModel = compilation.GetSemanticModel(tree);

            var type = tree.GetRoot().DescendantNodesAndSelf().OfType<TypeDeclarationSyntax>().First();

            return semanticModel.GetDeclaredSymbol(type);
        }
    }
}
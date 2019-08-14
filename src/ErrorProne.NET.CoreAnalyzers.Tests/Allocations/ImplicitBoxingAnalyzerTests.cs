﻿using ErrorProne.NET.CoreAnalyzers.Allocations;
using JetBrains.dotMemoryUnit;
using JetBrains.dotMemoryUnit.Kernel;
using NUnit.Framework;

namespace ErrorProne.NET.CoreAnalyzers.Tests.Allocations
{
    [TestFixture]
    public class ImplicitBoxingAnalyzerTests
    {
        static void VerifyCode(string code) => AllocationTestHelper.VerifyCode<ImplicitBoxingAllocationAnalyzer>(code);

        [Test]
        public void Calling_Overrides_Does_Not_Cause_Boxing()
        {
            VerifyCode(@"
struct S { 
    public override string ToString() => string.Empty;
    public override int GetHashCode() => 42;
    public override bool Equals(object other) => true;
}

class A {
    static S GetS() => default;
    static S MyS => default;

	static void Main() {
		S s = default;

        var hc2 = GetS().GetHashCode();
        var e2 = s.Equals(default);
        var str = MyS.ToString();
        var t = GetS().GetType();
	}
}");

            if (!dotMemoryApi.IsEnabled) return;

            StructWithOverrides s = default;

            var checkpoint = dotMemory.Check();

            var hc2 = s.GetHashCode();
            var e2 = s.Equals(default);
            var str2 = s.ToString();
            var t = s.GetType();

            dotMemory.Check(check =>
                Assert.AreEqual(
                    0,
                    check.GetDifference(checkpoint).GetNewObjects(where => where.Type.Is<StructWithOverrides>()).ObjectsCount));
        }

        [Test]
        public void Extension_Method_On_Struct_Should_Cause_No_Warnings()
        {
            VerifyCode(@"
interface I {}
struct S : I {}
static class E {
    public static void Foo(this S i) {}
}
class A {
    void M() {
        S s = default;
        s.Foo();
    }
}");
        }

        [Test]
        public void Calling_GetHashCode_On_Generic_Should_Not_Cause_Warning()
        {
            VerifyCode(@"
class A {
    // This should not cause any warnings.
    static int GenericGetHashCode<T>(T t) where T: struct => t.GetHashCode();

    static string GenericToString<T>(T t) where T: struct => t.ToString();
}

class GA<T> where T : struct
{
    static int GenericGetHashCode()
    {
        T t = default;
        return t.GetHashCode();
    }
}

");
        }

        [Test]
        public void Interface_Method_Call_Causes_No_Boxing_When_Called_Via_Generics_With_Constraints()
        {
            VerifyCode(@"
interface IFoo {void Foo();}
static class A {
	static void CallFoo<T>(T foo) where T : struct, IFoo => foo.Foo();
}");
        }

        [Test]
        public void Calling_Non_Override_On_Struct_Causes_Boxing()
        {
            VerifyCode(@"
namespace FooBar {
struct S { }
class A {
    static S GetS() => default;
    static S MyS => default;

    // This should not cause any warnings.
    static int GetHashCodeGeneric<T>(T t) where T: struct => t.GetHashCode();

	static void Main() {
		S s = default;

        string str2 = GetS().[|ToString|]();
        var hc3 = [|s.GetHashCode|]();
        var e3 = MyS.[|Equals|](default);
        var type2 = GetS().GetType(); // Should not warn

}}
}");

            if (!dotMemoryApi.IsEnabled) return;

            Struct s = default;

            var checkpoint = dotMemory.Check();

            var hc = s.GetHashCode();

            var checkpoint3 = dotMemory.Check(check =>
                Assert.AreEqual(
                    1,
                    check.GetDifference(checkpoint).GetNewObjects(where => @where.Type.Is<Struct>()).ObjectsCount));

            var e = s.Equals(default);

            var checkpoint4 = dotMemory.Check(check =>
                Assert.AreEqual(
                    1,
                    check.GetDifference(checkpoint3).GetNewObjects(where => where.Type.Is<Struct>()).ObjectsCount));

            var str = s.ToString();

            var checkpoint5 = dotMemory.Check(check =>
                Assert.AreEqual(
                    1,
                    check.GetDifference(checkpoint4).GetNewObjects(where => where.Type.Is<Struct>()).ObjectsCount));

            var t = s.GetType();

            dotMemory.Check(check =>
                Assert.AreEqual(
                    0,
                    check.GetDifference(checkpoint5).GetNewObjects(where => where.Type.Is<Struct>()).ObjectsCount));
        }

        [Test]
        public void HasFlag_Causes_Implicit_Boxing_Allocation()
        {
            VerifyCode(@"
[System.Flags]
enum E {
  V1 = 1,
  V2 = 1 << 1,
}

class A {
	static bool TestHasFlags(E e) => e.[|HasFlag|](E.V2);
}");

            if (!dotMemoryApi.IsEnabled) return;

            E e = default;
            var checkpoint = dotMemory.Check();

            var b = e.HasFlag(E.V1);

            dotMemory.Check(check =>
                Assert.AreEqual(
                    2, // e and E.V1 are both boxed
                    check.GetDifference(checkpoint).GetNewObjects(where => where.Type.Is<E>()).ObjectsCount));
        }
    }
}
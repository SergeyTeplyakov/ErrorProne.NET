﻿using System;
using System.Collections.Generic;
using NUnit.Framework;
using ErrorProne.NET.CoreAnalyzers.Allocations;
using JetBrains.dotMemoryUnit;
using JetBrains.dotMemoryUnit.Kernel;

namespace ErrorProne.NET.CoreAnalyzers.Tests.Allocations
{
    [TestFixture]
    public class ImplicitCastBoxingAnalyzerTests
    {
        static void VerifyCode(string code) =>
            AllocationTestHelper.VerifyCode<ImplicitBoxingAllocationAnalyzer>(code);

        [Test]
        public void ValueTuple_Conversion_Causes_Boxing() => VerifyCode(@"
using System;
struct S {}
static class A {
    static (S, S) _tuple = (default, default);
    static (object, object) M() => [|_tuple|];
    static (object, object) N() => ([|default(S)|], [|default(S)|]);
}");

        [Test]
        public void ValueTuple_Conversion_Causes_No_Boxing_For_User_Defined_Conversion() => VerifyCode(@"
public class C {}
public struct MyStruct
{
    public static implicit operator C(MyStruct ms) => null;
    public static (C, C) Test() => (new MyStruct(), new MyStruct());
    public static (object, object) Test2() => (new MyStruct(), new MyStruct());
}");

        [Test]
        public void Implicit_Conversion_In_String_Construction_Causes_Boxing()
        {
            VerifyCode(@"
using System;
using System.Collections.Generic;
using System.Linq;
struct S {}
static class A {
	static void Main()
    {
        int n = 42;

        string s = string.Empty + [|n|];

        string s2 = $""{[|n|]}"";
    }
}");
            if (!dotMemoryApi.IsEnabled) return;

            Struct s = default;

            var checkpoint = dotMemory.Check();

            string str1 = string.Empty + s;

            var checkpoint2 = dotMemory.Check(check =>
                Assert.AreEqual(
                    1,
                    check.GetDifference(checkpoint).GetNewObjects(where => where.Type.Is<Struct>()).ObjectsCount));

            string str2 = $"{s}";

            dotMemory.Check(check =>
                Assert.AreEqual(
                    1,
                    check.GetDifference(checkpoint2).GetNewObjects(where => where.Type.Is<Struct>()).ObjectsCount));
        }

        [Test]
        public void Implicit_Conversion_Causes_Boxing()
        {
            VerifyCode(@"
using System;
using System.Collections.Generic;
using System.Linq;
struct S {}
static class A {
	static void Main()
    {
        object o = [|42|];
        o = [|52|];
        IConvertible c = [|42|];
        c = [|52|];

        // argument conversion
        foo([|42|]);
        void foo(object arg) {}

        // return statement conversion
        object bar(int n) => [|n|];

        object x = string.Empty + [|42|];
    }
}");

            if (!dotMemoryApi.IsEnabled) return;

            var checkpoint = dotMemory.Check();

            object o = default(Struct);

            var checkpoint2 = dotMemory.Check(check =>
                Assert.AreEqual(
                    1,
                    check.GetDifference(checkpoint).GetNewObjects(where => where.Type.Is<Struct>()).ObjectsCount));

            o = default(Struct);

            var checkpoint3 = dotMemory.Check(check =>
                Assert.AreEqual(
                    1,
                    check.GetDifference(checkpoint2).GetNewObjects(where => where.Type.Is<Struct>()).ObjectsCount));

            IComparable c = default(ComparableStruct);

            var checkpoint4 = dotMemory.Check(check =>
                Assert.AreEqual(
                    1,
                    check.GetDifference(checkpoint3).GetNewObjects(where => where.Type.Is<ComparableStruct>())
                        .ObjectsCount));

            // argument conversion
            MemoryCheckPoint checkpoint5;

            void MethodTakesObject(object arg)
            {
                checkpoint5 = dotMemory.Check(check =>
                    Assert.AreEqual(
                        1,
                        check.GetDifference(checkpoint4).GetNewObjects(where => where.Type.Is<Struct>()).ObjectsCount));
            }

            MethodTakesObject(default(Struct));

            // return statement conversion
            object ReturnAsObject(Struct n) => n;
            o = ReturnAsObject(default);

            dotMemory.Check(check =>
                Assert.AreEqual(
                    1,
                    check.GetDifference(checkpoint5).GetNewObjects(where => where.Type.Is<Struct>()).ObjectsCount));
        }

        [Test]
        public void Implicit_Conversion_In_Params_Causes_Boxing()
        {
            VerifyCode(@"
static class A {
    static void Bar(params object[] p)
    {
    }
	static void Main()
    {
        Bar([|42|]);
    }
}");
            if (!dotMemoryApi.IsEnabled) return;

            var checkpoint = dotMemory.Check();

            Bar(default(Struct));

            void Bar(params object[] p)
            {
                dotMemory.Check(check =>
                    Assert.AreEqual(
                        1,
                        check.GetDifference(checkpoint).GetNewObjects(where => where.Type.Is<Struct>()).ObjectsCount));
            }
        }

        [Test]
        public void Implicit_Boxing_For_Delegate_Construction_From_Struct()
        {
            VerifyCode(@"
using System;
public class C {
 	public void M() {}   
}

public struct S {
    public void M() {
    }
    
    public static void StaticM() {}
    
    public static void Run()
    {
        C c = new C();
        S s = new S();
        
        // c is not boxed!
        Action a = c.M;
        a();
        
        // s is boxed
        a = new Action([|s|].M);
        a();

        // nothing to box
        a = new Action(S.StaticM);
        a();
    }
}");
            if (!dotMemoryApi.IsEnabled) return;

            Struct s = default;

            var checkpoint = dotMemory.Check();

            var a = new Action(s.Method);
            var a2 = new Action(Struct.StaticMethod);

            dotMemory.Check(check =>
                Assert.AreEqual(
                    1,
                    check.GetDifference(checkpoint).GetNewObjects(where => where.Type.Is<Struct>()).ObjectsCount));
        }

        [Test]
        public void Yield_Return_Int_Causes_Boxing()
        {
            VerifyCode(@"
using System;
using System.Collections.Generic;
using System.Linq;
struct S {}
static class A {
	static IEnumerable<object> Foo()
    {
        yield return [|42|];
    }
}");
            if (!dotMemoryApi.IsEnabled) return;

            var checkpoint = dotMemory.Check();

            foreach (object obj in GetEnumerable())
            {
                dotMemory.Check(check =>
                    Assert.AreEqual(
                        1,
                        check.GetDifference(checkpoint).GetNewObjects(where => where.Type.Is<Struct>()).ObjectsCount));
            }

            IEnumerable<object> GetEnumerable()
            {
                yield return default(Struct);
            }
        }

        [Test]
        public void Test_Implicit_Boxing_When_Foreach_Upcasts_Int_To_Object()
        {
            VerifyCode(@"
using System;
using System.Collections.Generic;
using System.Linq;
struct S {}
static class A {
	static void Main()
    {
        foreach(object obj in [|Enumerable.Range(1,10)|]) {}
        foreach(object obj in [|new int[0]|]) {}
    }
}");

            if (!dotMemoryApi.IsEnabled) return;

            var checkpoint = dotMemory.Check();

            foreach (object obj in new Struct[] {default})
            {
                dotMemory.Check(check =>
                    Assert.AreEqual(
                        1,
                        check.GetDifference(checkpoint).GetNewObjects(where => where.Type.Is<Struct>()).ObjectsCount));
            }
        }

        [Test]
        public void Interface_Method_Call_Causes_Boxing()
        {
            VerifyCode(@"
interface IFoo {void Foo();}
struct S : IFoo {public void Foo() {}}
static class A {
	static void CallFoo(IFoo foo) => foo.Foo();
    static void Main()
    {
        S s = default;
        CallFoo([|s|]);
    }
}");
        }

        [Test]
        public void Cast_To_ValueType()
        {
            VerifyCode(@"
struct S { }
class A {
    void M(){
        System.ValueType vt = [|default(S)|];
    }
}");
        }

        [Test]
        public void Cast_To_Enum()
        {
            VerifyCode(@"
enum E { A }
class A { 
    void M() { 
        System.Enum e = [|E.A|]; 
    } 
}");
        }

        [Test]
        public void Extension_Method_On_Interface_Causes_Boxing()
        {
            VerifyCode(@"
interface I {}
struct S : I {}
static class E {
    public static void Foo(this S i) {}
    public static void Bar(this I i) {}
}
class A {
    void M() {
        S s = default;
        s.Foo();
        [|s|].Bar();
    }
}");
        }

        [Test]
        public void Params_Arguments_Causes_Boxing() =>
            VerifyCode(@"
struct S {}
class A {
    static void N(params object[] parameters){}
    void M() {
        N([|1|], [|default(S)|], string.Empty);
    }
}");

        [Test]
        public void UserDefined_Implicit_Cast_Is_Ignored() =>
            VerifyCode(@"
struct S {
    public static implicit operator string(S s) => null;
}
class A {
    void M() {
        string str = default(S);
    }
}
");
    }
}
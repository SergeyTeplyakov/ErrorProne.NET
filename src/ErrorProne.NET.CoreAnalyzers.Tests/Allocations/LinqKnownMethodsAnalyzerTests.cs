using System;
using System.Collections.Generic;
using NUnit.Framework;
using ErrorProne.NET.CoreAnalyzers.Allocations;
using JetBrains.dotMemoryUnit;
using JetBrains.dotMemoryUnit.Kernel;

namespace ErrorProne.NET.CoreAnalyzers.Tests.Allocations
{
    [TestFixture]
    public class LinqKnownMethodsAnalyzerTests
    {
        static void VerifyCode(string code) => AllocationTestHelper.VerifyCode<LinqKnownMethodsAllocationAnalyzer>(code);

        [Test]
        public void Calling_Linq_Causes_Boxing() => VerifyCode(@"
using System.Linq;
struct S { }
class A {
	static void Main() {
		S[] arr = new S[1];
        S s = arr.[|First|]();
	}
}");

        [Test]
        public void Calling_Linq_Count_On_Collection_Does_Not_Cause_Boxing() => VerifyCode(@"
using System.Linq;
struct S { }
class A {
	static void Main() {
		S[] arr = new S[1];
        int c = arr.Count();
	}
}");
    }
}
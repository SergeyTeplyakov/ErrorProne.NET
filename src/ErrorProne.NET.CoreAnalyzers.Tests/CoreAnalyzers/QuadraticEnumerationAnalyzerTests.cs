using NUnit.Framework;
using System.Threading.Tasks;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.QuadraticEnumerationAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.CoreAnalyzers
{
    [TestFixture]
    public class QuadraticEnumerationAnalyzerTests
    {
        // -------- Q1: Same-symbol nested enumeration ---------------------------------------------------

        [Test]
        public async Task Q1_Warns_OnEnumerableCount_WithSameSource()
        {
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    void Foo(IEnumerable<int> coll) {
        foreach (var x in coll) {
            var n = [|coll.Count()|];
        }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task Q1_Warns_OnAny_WithSameSource()
        {
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    void Foo(IEnumerable<int> coll) {
        foreach (var x in coll) {
            if ([|coll.Any(y => y > x)|]) { }
        }
    }
}";
            await Verify.VerifyAsync(code);
        }

        // -------- Q2: Deferred re-enumeration in a loop ------------------------------------------------

        [Test]
        public async Task Q2_Warns_OnDeferredLocalInsideLoop()
        {
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    void Foo(IEnumerable<int> source, int[] outer) {
        IEnumerable<int> q = source.Where(x => x > 0);
        foreach (var i in outer) {
            var c = [|q.Count()|];
        }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task Q2_NoWarn_WhenLocalIsList()
        {
            // Same shape as Q2 but the local is concretely typed as List<int> — no deferred re-enum.
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    void Foo(int[] outer) {
        List<int> q = new List<int> { 1, 2, 3 };
        foreach (var i in outer) {
            var c = q.Count;
        }
    }
}";
            await Verify.VerifyAsync(code);
        }

        // -------- Q4: Nested foreach over the same source ----------------------------------------------

        [Test]
        public async Task Q4_Warns_OnNestedForeachOverSameLocal()
        {
            var code = @"
using System.Collections.Generic;
class Test {
    void Foo(List<int> coll) {
        foreach (var x in coll) {
            foreach (var y in [|coll|]) { }
        }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task Q4_NoWarn_OnNestedForeachOverDistinctSource()
        {
            var code = @"
using System.Collections.Generic;
class Test {
    void Foo(List<int> a, List<int> b) {
        foreach (var x in a) {
            foreach (var y in b) { }
        }
    }
}";
            await Verify.VerifyAsync(code);
        }

        // -------- Negative / boundary cases ------------------------------------------------------------

        [Test]
        public async Task NoWarn_OnSingleForeachNoInner()
        {
            var code = @"
using System.Collections.Generic;
class Test {
    void Foo(IEnumerable<int> coll) {
        foreach (var x in coll) { }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_OnEnumerableCount_OutsideLoop()
        {
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    void Foo(IEnumerable<int> coll) {
        var n = coll.Count();
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_OnForLoopUsingArrayLength()
        {
            // for (var i = 0; i < arr.Length; i++) — arr.Length is a property, not a method call.
            var code = @"
class Test {
    void Foo(int[] arr) {
        for (var i = 0; i < arr.Length; i++) {
            var v = arr[i];
        }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_OnListContainsInsideLoop_DifferentSource()
        {
            // After the v2 tightening EPC39 only flags re-iteration of the *same* enumerable. A generic
            // "List.Contains called in a loop on a different collection" pattern is no longer reported,
            // even though it is technically O(N·M) — too noisy in real codebases.
            var code = @"
using System.Collections.Generic;
class Test {
    void Foo(List<int> haystack, IEnumerable<int> needles) {
        foreach (var n in needles) {
            if (haystack.Contains(n)) { }
        }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task Q2_Warns_InsideWhileLoop()
        {
            // EPC39 fires for any loop kind, not just foreach.
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    void Foo(IEnumerable<int> source, int n) {
        IEnumerable<int> q = source.Where(x => x > 0);
        var i = 0;
        while (i < n) {
            var c = [|q.Count()|];
            i++;
        }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task Q2_NoWarn_AfterMaterialization()
        {
            // Materialize once before the loop — re-enumeration is now safe.
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    void Foo(IEnumerable<int> source, int[] outer) {
        var q = source.Where(x => x > 0).ToList();
        foreach (var i in outer) {
            var c = q.Count;
        }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task Q2_NoWarn_WhenDeferredLocalIsDeclaredInsideLoopBody()
        {
            // The deferred IEnumerable local is declared *inside* the loop body — a fresh enumerable is
            // bound every iteration, so multiple enumerations within a single iteration do NOT compound
            // with the outer loop size (it is a CA1851-style concern, not quadratic-in-the-loop).
            var code = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
class Test {
    void Foo(IEnumerable<Type> types) {
        foreach (var type in types) {
            IEnumerable<Attribute> attrs = type.GetCustomAttributes();
            var a = attrs.Any(x => x.GetType() == typeof(ObsoleteAttribute));
            var b = attrs.Any(x => x.GetType() == typeof(FlagsAttribute));
            var c = attrs.Any(x => x.GetType() == typeof(SerializableAttribute));
        }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task Q2_NoWarn_WhenDeferredLocalIsDeclaredInsideLoopBody_SingleEnumeration()
        {
            // Even a single enumeration on a deferred local declared inside the loop should not fire:
            // there is no outer-loop-multiplier to amortize away by hoisting.
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    void Foo(IEnumerable<int> source, int[] outer) {
        foreach (var i in outer) {
            IEnumerable<int> q = source.Where(x => x > i);
            var c = q.Count();
        }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task Q2_NoWarn_WhenDeferredLocalIsDeclaredInsideNestedBlockOfLoopBody()
        {
            // A local declared inside an inner `if` block (still syntactically inside the loop body) is
            // also fresh per iteration — must not warn.
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    void Foo(IEnumerable<int> source, int[] outer) {
        foreach (var i in outer) {
            if (i > 0) {
                IEnumerable<int> q = source.Where(x => x > i);
                var a = q.Any();
                var b = q.Count();
            }
        }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NestedLoops_OnlyInnerReports()
        {
            // Re-enumerating a deferred IEnumerable inside an inner loop should report ONCE (against the
            // inner loop's body), not twice (once for the outer and once for the inner).
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    void Foo(IEnumerable<int> source, IEnumerable<int> a, IEnumerable<int> b) {
        IEnumerable<int> q = source.Where(x => x > 0);
        foreach (var x in a) {
            foreach (var y in b) {
                var c = [|q.Count()|];
            }
        }
    }
}";
            await Verify.VerifyAsync(code);
        }
    }
}

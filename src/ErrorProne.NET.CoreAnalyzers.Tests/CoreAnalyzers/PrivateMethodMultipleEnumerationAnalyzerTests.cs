using NUnit.Framework;
using System.Threading.Tasks;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.PrivateMethodMultipleEnumerationAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.CoreAnalyzers
{
    [TestFixture]
    public class PrivateMethodMultipleEnumerationAnalyzerTests
    {
        [Test]
        public async Task Warns_OnLinqWhereChain()
        {
            // Canonical case: private helper enumerates `items` twice; caller passes `.Where(...)`.
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    private int Helper(IEnumerable<int> items) {
        var c = items.Count();
        return c + items.Sum();
    }
    void Caller(IEnumerable<int> source) {
        Helper([|source.Where(x => x > 0)|]);
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task Warns_OnSelectChain()
        {
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    private int Helper(IEnumerable<int> items) {
        var c = items.Count();
        foreach (var x in items) { c += x; }
        return c;
    }
    void Caller(IEnumerable<int> source) {
        Helper([|source.Select(x => x * 2)|]);
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task Warns_OnEnumerableRange()
        {
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    private int Helper(IEnumerable<int> items) {
        var a = items.Count();
        var b = items.Sum();
        return a + b;
    }
    void Caller() {
        Helper([|System.Linq.Enumerable.Range(0, 10)|]);
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task Warns_OnIteratorMethodResult()
        {
            // Caller passes the result of an iterator method (yield-based).
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    private int Helper(IEnumerable<int> items) {
        var c = items.Count();
        return c + items.Sum();
    }
    static IEnumerable<int> Produce() {
        yield return 1;
        yield return 2;
    }
    void Caller() {
        Helper([|Produce()|]);
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task Warns_ThroughOneLocalHop()
        {
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    private int Helper(IEnumerable<int> items) {
        var c = items.Count();
        return c + items.Sum();
    }
    void Caller(IEnumerable<int> source) {
        var query = source.Where(x => x > 0);
        Helper([|query|]);
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task Warns_ThroughTwoLocalHops()
        {
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    private int Helper(IEnumerable<int> items) {
        var c = items.Count();
        return c + items.Sum();
    }
    void Caller(IEnumerable<int> source) {
        var step1 = source.Where(x => x > 0);
        var step2 = step1;
        Helper([|step2|]);
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_OnList()
        {
            // Caller passes a materialized list; no re-enumeration penalty.
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    private int Helper(IEnumerable<int> items) {
        var c = items.Count();
        return c + items.Sum();
    }
    void Caller(List<int> source) {
        Helper(source);
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_OnArray()
        {
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    private int Helper(IEnumerable<int> items) {
        var c = items.Count();
        return c + items.Sum();
    }
    void Caller(int[] source) {
        Helper(source);
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_OnToListedQuery()
        {
            // Materialized — .ToList() breaks the deferred chain.
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    private int Helper(IEnumerable<int> items) {
        var c = items.Count();
        return c + items.Sum();
    }
    void Caller(IEnumerable<int> source) {
        Helper(source.Where(x => x > 0).ToList());
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_OnIEnumerableParameterReference()
        {
            // Caller forwards its own IEnumerable<T> parameter — provenance terminates at the parameter
            // and we cannot conclude the source is a query.
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    private int Helper(IEnumerable<int> items) {
        var c = items.Count();
        return c + items.Sum();
    }
    void Caller(IEnumerable<int> source) {
        Helper(source);
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_OnPublicHelper()
        {
            // Public helper — open world; closed-world assumption invalid; rule does not fire.
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    public int Helper(IEnumerable<int> items) {
        var c = items.Count();
        return c + items.Sum();
    }
    void Caller(IEnumerable<int> source) {
        Helper(source.Where(x => x > 0));
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_OnInternalHelper()
        {
            // V1 scope is private only; internal helpers are not tracked.
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    internal int Helper(IEnumerable<int> items) {
        var c = items.Count();
        return c + items.Sum();
    }
    void Caller(IEnumerable<int> source) {
        Helper(source.Where(x => x > 0));
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_OnSingleEnumerationInsideHelper()
        {
            // Helper enumerates `items` only once — not a candidate.
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    private int Helper(IEnumerable<int> items) {
        return items.Count();
    }
    void Caller(IEnumerable<int> source) {
        Helper(source.Where(x => x > 0));
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_WhenCallerCountExceedsCap()
        {
            // 4 distinct callers — past the cap of 3. Conservative: skip.
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    private int Helper(IEnumerable<int> items) {
        return items.Count() + items.Sum();
    }
    void A(IEnumerable<int> s) { Helper(s.Where(x => x > 0)); }
    void B(IEnumerable<int> s) { Helper(s.Where(x => x > 0)); }
    void C(IEnumerable<int> s) { Helper(s.Where(x => x > 0)); }
    void D(IEnumerable<int> s) { Helper(s.Where(x => x > 0)); }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task Warns_MixedCallers_OnlyOffendingOneReported()
        {
            // Two callers; one passes a query, one passes a list. Report only the query caller.
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    private int Helper(IEnumerable<int> items) {
        return items.Count() + items.Sum();
    }
    void Good(List<int> s) { Helper(s); }
    void Bad(IEnumerable<int> s) { Helper([|s.Where(x => x > 0)|]); }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task Warns_OnForeachAndSumPattern()
        {
            // Helper enumerates `items` via foreach AND a LINQ call.
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    private int Helper(IEnumerable<int> items) {
        var s = items.Sum();
        foreach (var x in items) { s += x; }
        return s;
    }
    void Caller(IEnumerable<int> source) {
        Helper([|source.Select(x => x + 1)|]);
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_OnUnclassifiedArgument()
        {
            // Argument is a property access — we can't classify it as a query.
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    private System.Collections.Generic.IEnumerable<int> Source { get; set; } = new int[] { 1, 2, 3 };
    private int Helper(IEnumerable<int> items) {
        return items.Count() + items.Sum();
    }
    void Caller() {
        Helper(Source);
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task Warns_ThreeCallers_AllReported()
        {
            var code = @"
using System.Collections.Generic;
using System.Linq;
class Test {
    private int Helper(IEnumerable<int> items) {
        return items.Count() + items.Sum();
    }
    void A(IEnumerable<int> s) { Helper([|s.Where(x => x > 0)|]); }
    void B(IEnumerable<int> s) { Helper([|s.Where(x => x > 0)|]); }
    void C(IEnumerable<int> s) { Helper([|s.Where(x => x > 0)|]); }
}";
            await Verify.VerifyAsync(code);
        }
    }
}

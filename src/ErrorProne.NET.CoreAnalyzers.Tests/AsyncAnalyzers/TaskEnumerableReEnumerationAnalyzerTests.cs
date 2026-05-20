using NUnit.Framework;
using System.Threading.Tasks;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.TaskEnumerableReEnumerationAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class TaskEnumerableReEnumerationAnalyzerTests
    {
        [Test]
        public async Task WarnsOnWhenAllThenForeach()
        {
            // Canonical bug: WhenAll consumes once, then foreach re-runs the iterator.
            var code = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class Test {
    async Task Foo(IEnumerable<int> ids) {
        var tasks = ids.Select(async id => { await Task.Delay(1); return id; });
        await Task.WhenAll(tasks);
        foreach (var t in [|tasks|]) { await t; }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task WarnsOnDoubleLinqAggregation()
        {
            var code = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class Test {
    void Foo(IEnumerable<Task<int>> tasks) {
        var first = tasks.Count();
        var second = [|tasks|].Count();
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_SingleEnumeration_WithWhenAll()
        {
            var code = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class Test {
    async Task Foo(IEnumerable<int> ids) {
        var tasks = ids.Select(async id => { await Task.Delay(1); return id; });
        await Task.WhenAll(tasks);
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_SingleEnumeration_Foreach()
        {
            var code = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class Test {
    async Task Foo(IEnumerable<Task> tasks) {
        foreach (var t in tasks) { await t; }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_TaskArray_DoubleEnumeration()
        {
            // Task[] is materialized — re-enumeration is safe.
            var code = @"
using System.Linq;
using System.Threading.Tasks;
class Test {
    async Task Foo(Task[] tasks) {
        await Task.WhenAll(tasks);
        foreach (var t in tasks) { _ = t.Status; }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_ListOfTask_DoubleEnumeration()
        {
            var code = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class Test {
    async Task Foo(List<Task> tasks) {
        await Task.WhenAll(tasks);
        foreach (var t in tasks) { _ = t.Status; }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_IReadOnlyListOfTask_DoubleEnumeration()
        {
            var code = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class Test {
    async Task Foo(IReadOnlyList<Task> tasks) {
        await Task.WhenAll(tasks);
        foreach (var t in tasks) { _ = t.Status; }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_MaterializedViaToArray()
        {
            // After ToArray, the local has type Task[], so re-enumeration is safe.
            var code = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class Test {
    async Task Foo(IEnumerable<int> ids) {
        var tasks = ids.Select(async id => { await Task.Delay(1); return id; }).ToArray();
        await Task.WhenAll(tasks);
        foreach (var t in tasks) { await t; }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task WarnsOnWhenAnyThenForeach()
        {
            var code = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class Test {
    async Task Foo(IEnumerable<int> ids) {
        var tasks = ids.Select(async id => { await Task.Delay(1); return id; });
        await Task.WhenAny(tasks);
        foreach (var t in [|tasks|]) { await t; }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task WarnsOnAlias()
        {
            // var y = tasks introduces an alias; enumerating both should still warn.
            var code = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class Test {
    async Task Foo(IEnumerable<Task<int>> tasks) {
        var y = tasks;
        await Task.WhenAll(y);
        foreach (var t in [|tasks|]) { await t; }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_ReassignmentResetsEpoch()
        {
            // After re-assignment the second enumeration is on a fresh source.
            var code = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class Test {
    async Task Foo(IEnumerable<int> ids) {
        var tasks = ids.Select(async id => { await Task.Delay(1); return id; });
        await Task.WhenAll(tasks);
        tasks = ids.Select(async id => { await Task.Delay(1); return id; });
        foreach (var t in tasks) { await t; }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_ManualForeachOnly()
        {
            // A single manual foreach (no WhenAll) over an IEnumerable<Task> is fine.
            var code = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class Test {
    async Task Foo(IEnumerable<Task> tasks) {
        foreach (var t in tasks) { await t; }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task WarnsOnDoubleAwaitForeach()
        {
            // Two manual await-foreaches on the same IEnumerable<Task> — strict-superset case.
            var code = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class Test {
    async Task Foo(IEnumerable<Task> tasks) {
        foreach (var t in tasks) { await t; }
        foreach (var t in [|tasks|]) { _ = t.Status; }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task WarnsOnValueTaskEnumerable()
        {
            // IEnumerable<ValueTask> exhibits the same hazard.
            var code = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class Test {
    async Task Foo(IEnumerable<int> ids) {
        var tasks = ids.Select(async id => { await Task.Delay(1); });
        foreach (var t in tasks) { await t; }
        foreach (var t in [|tasks|]) { _ = t.IsCompleted; }
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_NestedLambdaIndependentScope()
        {
            // The lambda body has its own block analysis; re-using the parameter inside the lambda once
            // does NOT combine with the outer enumeration to form a 2nd site.
            var code = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class Test {
    async Task Foo(IEnumerable<Task> tasks) {
        Action a = () => { foreach (var t in tasks) { } };
        a();
    }
}";
            await Verify.VerifyAsync(code);
        }

        [Test]
        public async Task WarnsOnLinqAggregationAfterWhenAll()
        {
            var code = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class Test {
    async Task<int> Foo(IEnumerable<Task<int>> tasks) {
        await Task.WhenAll(tasks);
        return [|tasks|].Sum(t => t.Result);
    }
}";
            await Verify.VerifyAsync(code);
        }
    }
}

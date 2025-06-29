using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.AsyncAnalyzers.TaskCompletionSourceRunContinuationsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class TaskCompletionSourceRunContinuationsAnalyzerTests
    {
        [Test]
        public async Task Warn_When_TaskCompletionSource_Of_T_Created_Without_RunContinuationsAsynchronously()
        {
            var test = @"
using System.Threading.Tasks;
class Test {
    void Foo() {
        var tcs = [|new TaskCompletionSource<int>()|];
    }
}
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task Warn_When_TaskCompletionSource_Created_Without_RunContinuationsAsynchronously()
        {
            var test = @"
using System.Threading.Tasks;
class Test {
    void Foo() {
        var tcs = [|new TaskCompletionSource()|];
    }
}
";
            var t = new Verify.Test { TestCode = test };
            t.ReferenceAssemblies = ReferenceAssemblies.Net.Net90;
            await t.RunAsync();
        }

        [Test]
        public async Task NoWarn_When_TaskCompletionSource_Created_With_RunContinuationsAsynchronously()
        {
            var test = @"
using System.Threading.Tasks;
class Test {
    void Foo() {
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task Warn_When_TaskCompletionSource_Created_With_Other_Options()
        {
            var test = @"
using System.Threading.Tasks;
class Test {
    void Foo() {
        var tcs = [|new TaskCompletionSource<int>(TaskCreationOptions.None)|];
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarn_When_TaskCompletionSource_NonGeneric_Created_With_RunContinuationsAsynchronously()
        {
            var test = @"
using System.Threading.Tasks;
class Test {
    void Foo() {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
";
            var t = new Verify.Test { TestCode = test };
            t.ReferenceAssemblies = ReferenceAssemblies.Net.Net90;
            await t.RunAsync();
        }

        [Test]
        public async Task NoWarn_When_TaskCompletionSource_Created_With_Combined_RunContinuationsAsynchronously()
        {
            var test = @"
using System.Threading.Tasks;
class Test {
    void Foo() {
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously | TaskCreationOptions.AttachedToParent);
    }
}
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task Warn_When_TaskCompletionSource_Created_With_Options_From_Parameter()
        {
            var test = @"
using System.Threading.Tasks;
class Test {
    void Foo(TaskCreationOptions options) {
        var tcs = [|new TaskCompletionSource<int>(options)|];
    }
}
";
            await Verify.VerifyAsync(test);
        }
    }
}

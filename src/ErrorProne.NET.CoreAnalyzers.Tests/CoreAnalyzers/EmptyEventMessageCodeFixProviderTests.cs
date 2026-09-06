using NUnit.Framework;
using System.Threading.Tasks;
using ErrorProne.NET.EventSourceAnalysis;
using ErrorProne.NET.TestHelpers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.EventSourceAnalysis.EmptyEventMessageAnalyzer,
    ErrorProne.NET.EventSourceAnalysis.EmptyEventMessageCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.AsyncAnalyzers
{
    [TestFixture]
    public class EmptyEventMessageCodeFixProviderTests
    {
        private static async Task VerifyFixAsync(string code, string fixedCode, string equivalenceKey)
        {
            var test = new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                LanguageVersion = LanguageVersion.Latest,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                CodeActionEquivalenceKey = equivalenceKey,
            };

            await test.WithoutGeneratedCodeVerification().RunAsync();
        }

        [Test]
        public async Task Fix_Empty_Constant_Expressions(
            [Values("EmptyConst", "(EmptyConst)", "EmptyConst + \"\"", "$\"{EmptyConst}\"")] string expression,
            [Values("Message", "@Message", "\\u004dessage")] string argumentName,
            [Values(EmptyEventMessageCodeFixProvider.RemoveMessageTitle, EmptyEventMessageCodeFixProvider.UseSingleSpaceTitle)] string equivalenceKey)
        {
            string code = $@"
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{{
    private const string EmptyConst = """";

    [System.Diagnostics.Tracing.Event(1, [|{argumentName} = {expression}|])]
    public void AppStarted(string m) => WriteEvent(1, m);
}}";

            var fixedArgument = equivalenceKey == EmptyEventMessageCodeFixProvider.RemoveMessageTitle
                ? ""
                : $", {argumentName} = \" \"";
            string expected = $@"
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{{
    private const string EmptyConst = """";

    [System.Diagnostics.Tracing.Event(1{fixedArgument})]
    public void AppStarted(string m) => WriteEvent(1, m);
}}";

            await VerifyFixAsync(code, expected, equivalenceKey);
        }

        [Test]
        public async Task Fix_Multiple_Events(
            [Values(EmptyEventMessageCodeFixProvider.RemoveMessageTitle, EmptyEventMessageCodeFixProvider.UseSingleSpaceTitle)] string equivalenceKey)
        {
            string code = @"
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    private const string EmptyConst = """";

    [System.Diagnostics.Tracing.Event(1, [|Message = EmptyConst|])]
    public void AppStarted(string m) => WriteEvent(1, m);

    [System.Diagnostics.Tracing.Event(2, [|Message = """"|])]
    public void AppStopped(string m) => WriteEvent(2, m);
}";

            var fixedArgument = equivalenceKey == EmptyEventMessageCodeFixProvider.RemoveMessageTitle
                ? ""
                : ", Message = \" \"";
            string expected = $@"
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{{
    private const string EmptyConst = """";

    [System.Diagnostics.Tracing.Event(1{fixedArgument})]
    public void AppStarted(string m) => WriteEvent(1, m);

    [System.Diagnostics.Tracing.Event(2{fixedArgument})]
    public void AppStopped(string m) => WriteEvent(2, m);
}}";

            await VerifyFixAsync(code, expected, equivalenceKey);
        }

        [Test]
        public async Task Remove_Message_Argument_When_Last()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1, Level = System.Diagnostics.Tracing.EventLevel.Error, [|Message = """"|])]
    public void AppStarted(string m) => WriteEvent(1, m);
}";

            string expected = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1, Level = System.Diagnostics.Tracing.EventLevel.Error)]
    public void AppStarted(string m) => WriteEvent(1, m);
}";

            await VerifyFixAsync(code, expected, EmptyEventMessageCodeFixProvider.RemoveMessageTitle);
        }

        [Test]
        public async Task Remove_Message_Argument_When_Only_Named_Arg()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1, [|Message = """"|])]
    public void AppStarted(string m) => WriteEvent(1, m);
}";

            string expected = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1)]
    public void AppStarted(string m) => WriteEvent(1, m);
}";

            await VerifyFixAsync(code, expected, EmptyEventMessageCodeFixProvider.RemoveMessageTitle);
        }

        [Test]
        public async Task Remove_Message_Argument_When_Middle()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1, [|Message = """"|], Level = System.Diagnostics.Tracing.EventLevel.Error)]
    public void AppStarted(string m) => WriteEvent(1, m);
}";

            string expected = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1, Level = System.Diagnostics.Tracing.EventLevel.Error)]
    public void AppStarted(string m) => WriteEvent(1, m);
}";

            await VerifyFixAsync(code, expected, EmptyEventMessageCodeFixProvider.RemoveMessageTitle);
        }

        [Test]
        public async Task Use_Single_Space()
        {
            string code = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1, [|Message = """"|])]
    public void AppStarted(string m) => WriteEvent(1, m);
}";

            string expected = @"
[System.Diagnostics.Tracing.EventSource(Name = ""Demo"")]
public sealed class DemoEventSource : System.Diagnostics.Tracing.EventSource
{
    [System.Diagnostics.Tracing.Event(1, Message = "" "")]
    public void AppStarted(string m) => WriteEvent(1, m);
}";

            await VerifyFixAsync(code, expected, EmptyEventMessageCodeFixProvider.UseSingleSpaceTitle);
        }
    }
}

using System.Threading.Tasks;
using ErrorProne.NET.TestHelpers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.FormatMethodArgumentsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.CoreAnalyzers
{
    [TestFixture]
    public class FormatMethodArgumentsAnalyzerTests
    {
        private const string LoggerStub = @"
namespace MyCorp.Logging {
    public static class Logger {
        public static void Log(string format, params object[] args) { }
        public static void LogWithProvider(System.IFormatProvider provider, string format, params object[] args) { }
        public static void LogTwo(string format, object a, object b) { }
        public static void LogNoArgs(string format) { }
    }
}
";

        private static Task RunAsync(string source, string editorConfig, params DiagnosticResult[] expected)
        {
            var test = new Verify.Test
            {
                LanguageVersion = LanguageVersion.Latest,
                TestState =
                {
                    Sources = { source, LoggerStub },
                    AnalyzerConfigFiles = { ("/.editorconfig", editorConfig) },
                },
            }.WithoutGeneratedCodeVerification();

            foreach (var diag in expected)
            {
                test.ExpectedDiagnostics.Add(diag);
            }

            return test.RunAsync();
        }

        private static string EditorConfig(string formatMethods) =>
$@"root = true
[*.cs]
dotnet_diagnostic.epc41.format_methods = {formatMethods}
";

        [Test]
        public async Task NoWarn_WhenNotConfigured()
        {
            // No editorconfig entries → the rule is dormant; even a clearly-broken call is silent.
            var code = @"
using MyCorp.Logging;
class C { void M() { Logger.Log(""{0} {1}"", 1); } }";

            await RunAsync(code, editorConfig: "");
        }

        [Test]
        public async Task NoWarn_WhenMethodNotInAllowlist()
        {
            // The configured method name does not match the called method → no warning.
            var code = @"
using MyCorp.Logging;
class C { void M() { Logger.Log(""{0} {1}"", 1); } }";

            await RunAsync(code, EditorConfig("MyCorp.Logging.Logger.LogTwo:0"));
        }

        [Test]
        public async Task Warn_PlaceholderIndexExceedsArgCount()
        {
            // Format references {0} and {1} but only one arg supplied → warn.
            var code = @"
using MyCorp.Logging;
class C {
    void M() {
        {|EPC41:Logger.Log(""{0} {1}"", 1)|};
    }
}";

            await RunAsync(code, EditorConfig("MyCorp.Logging.Logger.Log:0"));
        }

        [Test]
        public async Task Warn_ZeroArgsButPlaceholderUsed()
        {
            var code = @"
using MyCorp.Logging;
class C {
    void M() {
        {|EPC41:Logger.Log(""value = {0}"")|};
    }
}";

            await RunAsync(code, EditorConfig("MyCorp.Logging.Logger.Log:0"));
        }

        [Test]
        public async Task NoWarn_WhenAllPlaceholdersHaveArgs()
        {
            var code = @"
using MyCorp.Logging;
class C {
    void M() {
        Logger.Log(""{0} and {1}"", 1, 2);
    }
}";

            await RunAsync(code, EditorConfig("MyCorp.Logging.Logger.Log:0"));
        }

        [Test]
        public async Task NoWarn_WhenFormatHasNoPlaceholders()
        {
            var code = @"
using MyCorp.Logging;
class C {
    void M() {
        Logger.Log(""no placeholders here"");
    }
}";

            await RunAsync(code, EditorConfig("MyCorp.Logging.Logger.Log:0"));
        }

        [Test]
        public async Task NoWarn_OnEscapedBraces()
        {
            // '{{' / '}}' are literal braces, not placeholders.
            var code = @"
using MyCorp.Logging;
class C {
    void M() {
        Logger.Log(""{{not a placeholder}} {0}"", 42);
    }
}";

            await RunAsync(code, EditorConfig("MyCorp.Logging.Logger.Log:0"));
        }

        [Test]
        public async Task Warn_OnMalformedFormat_UnbalancedOpenBrace()
        {
            var code = @"
using MyCorp.Logging;
class C {
    void M() {
        Logger.Log({|EPC41:""unterminated {0""|}, 1);
    }
}";

            await RunAsync(code, EditorConfig("MyCorp.Logging.Logger.Log:0"));
        }

        [Test]
        public async Task Warn_OnMalformedFormat_StrayCloseBrace()
        {
            var code = @"
using MyCorp.Logging;
class C {
    void M() {
        Logger.Log({|EPC41:""stray } here""|}, 1);
    }
}";

            await RunAsync(code, EditorConfig("MyCorp.Logging.Logger.Log:0"));
        }

        [Test]
        public async Task NoWarn_WhenFormatIsNotConstant()
        {
            // Runtime-built format strings cannot be analyzed.
            var code = @"
using MyCorp.Logging;
class C {
    void M(string fmt) {
        Logger.Log(fmt, 1, 2);
    }
}";

            await RunAsync(code, EditorConfig("MyCorp.Logging.Logger.Log:0"));
        }

        [Test]
        public async Task NoWarn_WhenParamsArrayIsOpaque()
        {
            // Caller passes an existing object[] variable; we cannot count statically → no report.
            var code = @"
using MyCorp.Logging;
class C {
    void M(object[] xs) {
        Logger.Log(""{0} {1} {2}"", xs);
    }
}";

            await RunAsync(code, EditorConfig("MyCorp.Logging.Logger.Log:0"));
        }

        [Test]
        public async Task Warn_WithExplicitArrayLiteral_TooFewElements()
        {
            // Explicit `new object[] { ... }` literal — we can count.
            var code = @"
using MyCorp.Logging;
class C {
    void M() {
        {|EPC41:Logger.Log(""{0} {1} {2}"", new object[] { 1, 2 })|};
    }
}";

            await RunAsync(code, EditorConfig("MyCorp.Logging.Logger.Log:0"));
        }

        [Test]
        public async Task Warn_WithFormatProviderOverload_FormatIndex1()
        {
            // Format param is at index 1 (after IFormatProvider).
            var code = @"
using System.Globalization;
using MyCorp.Logging;
class C {
    void M() {
        {|EPC41:Logger.LogWithProvider(CultureInfo.InvariantCulture, ""{0} {1}"", 1)|};
    }
}";

            await RunAsync(code, EditorConfig("MyCorp.Logging.Logger.LogWithProvider:1"));
        }

        [Test]
        public async Task Warn_WithFixedTrailingParams_NotParamsArray()
        {
            // Method signature uses two fixed object parameters (no params array).
            var code = @"
using MyCorp.Logging;
class C {
    void M() {
        {|EPC41:Logger.LogTwo(""{0} {1} {2}"", 1, 2)|};
    }
}";

            await RunAsync(code, EditorConfig("MyCorp.Logging.Logger.LogTwo:0"));
        }

        [Test]
        public async Task Warn_WithLogNoArgs_OverloadHasNoArgs()
        {
            // Single-parameter format method: any placeholder is automatically out of range.
            var code = @"
using MyCorp.Logging;
class C {
    void M() {
        {|EPC41:Logger.LogNoArgs(""hello {0}"")|};
    }
}";

            await RunAsync(code, EditorConfig("MyCorp.Logging.Logger.LogNoArgs:0"));
        }

        [Test]
        public async Task NoWarn_FormatItemWithAlignmentAndSpec_InRange()
        {
            var code = @"
using MyCorp.Logging;
class C {
    void M() {
        Logger.Log(""[{0,10:N2}] [{1,-5}]"", 3.14, ""x"");
    }
}";

            await RunAsync(code, EditorConfig("MyCorp.Logging.Logger.Log:0"));
        }

        [Test]
        public async Task MultipleEntries_OneMatches()
        {
            // Several entries in config — pick the right one for the called method.
            var code = @"
using MyCorp.Logging;
class C {
    void M() {
        {|EPC41:Logger.Log(""{0}"")|};
        Logger.LogTwo(""{0} {1}"", 1, 2);
    }
}";

            await RunAsync(code, EditorConfig("MyCorp.Logging.Logger.Log:0;MyCorp.Logging.Logger.LogTwo:0"));
        }

        [Test]
        public async Task Warn_WhenPatternMatchesMethod()
        {
            var code = @"
using MyCorp.Logging;
class C {
    void M() {
        {|EPC41:Logger.Log(""{0} {1}"", 1)|};
    }
}";

            await RunAsync(code, EditorConfig("MyCorp.Logging.Logger.Log*:0"));
        }

        [Test]
        public async Task Warn_WhenNamedParameterUsed()
        {
            var code = @"
using MyCorp.Logging;
class C {
    void M() {
        {|EPC41:Logger.Log(format: ""{0} {1}"", args: new object[] { 1 })|};
    }
}";

            await RunAsync(code, EditorConfig("MyCorp.Logging.Logger.Log:format"));
        }

        [Test]
        public async Task NoWarn_WhenPatternDoesNotMatch()
        {
            var code = @"
using MyCorp.Logging;
class C {
    void M() {
        Logger.Log(""{0} {1}"", 1);
    }
}";

            await RunAsync(code, EditorConfig("MyCorp.Logging.Logger.LogTwo:0"));
        }
    }
}

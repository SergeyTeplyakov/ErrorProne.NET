using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.CodeAnalysis.Testing;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.ExcludeFromCodeCoverageMessageAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class ExcludeFromCodeCoverageMessageAnalyzerTests
    {
        [Test]
        public async Task WarnsWhenNoMessageProvided_Net90()
        {
            var test = @"
using System.Diagnostics.CodeAnalysis;
[[|ExcludeFromCodeCoverage|]]
public class C { }
";
            var t = new Verify.Test { TestCode = test };
            t.ReferenceAssemblies = ReferenceAssemblies.Net.Net90;
            await t.RunAsync();
        }

        [Test]
        public async Task NoWarningWhenMessageProvided_Net90()
        {
            var test = @"
using System.Diagnostics.CodeAnalysis;
[ExcludeFromCodeCoverage(Justification = ""reason"")]
public class C { }
";
            var t = new Verify.Test { TestCode = test };
            t.ReferenceAssemblies = ReferenceAssemblies.Net.Net90;
            await t.RunAsync();
        }

        [Test]
        public async Task NoWarningWhenJustificationNotAvailable_NetStandard20()
        {
            // There is no Justification property in ExcludeFromCodeCoverageAttribute in .NET Standard 2.0
            var test = @"
using System.Diagnostics.CodeAnalysis;
[ExcludeFromCodeCoverage]
public class C { }
";
            var t = new Verify.Test { TestCode = test };
            t.ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20;
            await t.RunAsync();
        }
    }
}

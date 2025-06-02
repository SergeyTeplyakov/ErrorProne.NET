using NUnit.Framework;
using System.Threading.Tasks;
using ErrorProne.NET.CoreAnalyzers;
using Verify = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.ExcludeFromCodeCoverageOnPartialClassAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class ExcludeFromCodeCoverageOnPartialClassAnalyzerTests
    {
        [Test]
        public async Task WarnsWhenAttributeAppliedDirectlyToPartialClass()
        {
            var test = @"
using System.Diagnostics.CodeAnalysis;
[[|ExcludeFromCodeCoverage|]]
public partial class C { }
public partial class C { }
";
            await Verify.VerifyAsync(test);
        }
        
        [Test]
        public async Task WarnsWhenAttributeAppliedToOnePartOfPartialClass()
        {
            var test = @"
using System.Diagnostics.CodeAnalysis;
public partial class C { }
[[|ExcludeFromCodeCoverage|]]
public partial class C { }
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarningOnNonPartialClass()
        {
            var test = @"
using System.Diagnostics.CodeAnalysis;
[ExcludeFromCodeCoverage]
public class C { }
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarningWhenAttributeAppliedToMethod()
        {
            var test = @"
using System.Diagnostics.CodeAnalysis;
public partial class C {
    [ExcludeFromCodeCoverage]
    public void M() { }
}
public partial class C { }
";
            await Verify.VerifyAsync(test);
        }

        [Test]
        public async Task NoWarningWhenAttributeAppliedToProperty()
        {
            var test = @"
using System.Diagnostics.CodeAnalysis;
public partial class C {
    [ExcludeFromCodeCoverage]
    public int P { get; set; }
}
public partial class C { }
";
            await Verify.VerifyAsync(test);
        }
    }
}

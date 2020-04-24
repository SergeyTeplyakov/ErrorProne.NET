using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.StringBuilderAppendFormatAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class StringBuilderAppendFormatAnalyzerTests
    {
        [Test]
        public async Task Warn_On_Custom_Object()
        {
            string code = @"
public class MyClass
{
  public override string ToString() => string.Empty;
  public static string S()
  {
    var sb = new System.Text.StringBuilder();
    sb.AppendFormat($""{new MyClass()}"");
  }
}
";

            await VerifyCS.VerifyAsync(code);
        }
        
        [Test]
        public async Task Warn_When_AppendFormat_Doesnt_Take_Extra_Objects()
        {
            string code = @"
public class MyClass
{
  public static string S()
  {
    var sb = new System.Text.StringBuilder();
    [|sb.AppendFormat("""")|];
  }
}
";

            await VerifyCS.VerifyAsync(code);
        }
    }
}
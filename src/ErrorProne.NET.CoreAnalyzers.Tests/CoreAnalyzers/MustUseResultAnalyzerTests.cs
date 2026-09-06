using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.MustUseResultAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests
{
    [TestFixture]
    public class MustUseResultAnalyzerTests
    {
        [Test]
        public async Task Method_With_MustUseResultAttribute_Should_Be_Observed()
        {
            string code = """
[System.AttributeUsage(System.AttributeTargets.Method)]
public class MustUseResultAttribute : System.Attribute { }

public class Runner
{
  [MustUseResult]
  public static int GetValue() => 42;

  public static void Test()
  {
      [|GetValue|]();
  }
}
""";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Method_With_MustUseResultAttribute_Used_Should_Not_Warn()
        {
            string code = """
[System.AttributeUsage(System.AttributeTargets.Method)]
public class MustUseResultAttribute : System.Attribute { }

public class Runner
{
  [MustUseResult]
  public static int GetValue() => 42;

  public static void Test()
  {
      var result = GetValue();
  }
}
""";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }
        
        [Test]
        public async Task Method_With_MustUseResultAttribute_Can_Be_Observed_With_Suppression()
        {
            // This is something that I'm not sure if it's ok to observe the result
            // with _ = fooBar();
            // But for now, it is possible.
            string code = """
[System.AttributeUsage(System.AttributeTargets.Method)]
public class MustUseResultAttribute : System.Attribute { }

public class Runner
{
  [MustUseResult]
  public static int GetValue() => 42;

  public static void Test()
  {
      _ = GetValue();
  }
}
""";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task Method_Without_MustUseResultAttribute_Should_Not_Warn()
        {            string code = """
public class Runner
{
   public static int GetValue() => 42;

   public static void Test()
   {
       if (GetValue() > 0)
       {
           // Method result is used in the condition
       }
   }
}
""";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task Method_With_MustUseResultAttribute_In_Expression_Should_Not_Warn()
        {
            string code = """
[System.AttributeUsage(System.AttributeTargets.Method)]
public class MustUseResultAttribute : System.Attribute { }

public class Runner
{
    [MustUseResult]
    public static int GetValue() => 42;

    public static void Test()
    {
        if (GetValue() > 0)
        {
            // Method result is used in the condition
        }
    }
}
""";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task Async_Method_With_MustUseResultAttribute_Should_Be_Observed()
        {
            string code = @"
using System.Threading.Tasks;
[System.AttributeUsage(System.AttributeTargets.Method)]
public class MustUseResultAttribute : System.Attribute { }

public class Runner
{
    [MustUseResult]
    public static async Task<int> GetValueAsync() => await Task.FromResult(42);

    public static async Task Test()
    {
        [|await GetValueAsync()|];
    }
}
";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Async_Method_With_MustUseResultAttribute_Used_Should_Not_Warn()
        {
            string code = """
using System.Threading.Tasks;
[System.AttributeUsage(System.AttributeTargets.Method)]
public class MustUseResultAttribute : System.Attribute { }

public class Runner
{
    [MustUseResult]
    public static async Task<int> GetValueAsync() => await Task.FromResult(42);

    public static async Task Test()
    {
        var result = await GetValueAsync();
    }
}
""";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task Override_Method_With_MustUseResultAttribute_Should_Be_Observed()
        {
            string code = """
[System.AttributeUsage(System.AttributeTargets.Method)]
public class MustUseResultAttribute : System.Attribute { }

public abstract class BaseClass
{
    [MustUseResult]
    public abstract int GetValue();
}

public class DerivedClass : BaseClass
{
    public override int GetValue() => 42;
}
public class Runner
{
    public static void Test()
    {
        var derived = new DerivedClass();
        derived.[|GetValue|]();
    }
}
""";
            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Interface_Method_With_MustUseResultAttribute_Should_Be_Observed()
        {
            string code = """
[System.AttributeUsage(System.AttributeTargets.Method)]
public class MustUseResultAttribute : System.Attribute { }

public interface IValueProvider
{
    [MustUseResult]
    int GetValue();
}

public class ValueProvider : IValueProvider
{
    public int GetValue() => 42;
}
public class Runner
{
    public static void Test()
    {
        IValueProvider provider = new ValueProvider();
        provider.[|GetValue|]();
    }
}
""";
            await VerifyCS.VerifyAsync(code);
        }
    }
}

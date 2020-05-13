namespace ErrorProne.NET.StructAnalyzers.Tests.Samples
{
    public struct IdAndName
    {
        public int Id { get; }
        public string Name { get; }
    }

public readonly struct RS
{
    public static void UsedAsIn(RS rs)
    {
        UseAsOut(ref rs);
    }

    public static void UseAsOut(ref RS rs) => rs = default;
}

    public readonly struct S
    {
        public static void Sample()
        {
            S s = default;
            s.Foo();
            s.Baz();
            s.Bar();
        }
    }


    public static class C
    {
        // Extension methods for value types
        // (or for generic type parameters with the struct constrained)
        // can take 'this' by value, by 'in' or by 'ref'.
        public static void Bar(this S s) { }
        public static void Foo(in this S s) { }   
        public static void Baz(ref this S s) { }
    }
    



    //public class AsyncSample
    //{
    //    public async Task FooAsync(in int x)
    //    {
    //    }
    //}
}
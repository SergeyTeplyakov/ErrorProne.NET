using System.Threading.Tasks;

namespace ErrorProne.NET.Structs.Tests.Samples
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

    

    //public class AsyncSample
    //{
    //    public async Task FooAsync(in int x)
    //    {
    //    }
    //}
}
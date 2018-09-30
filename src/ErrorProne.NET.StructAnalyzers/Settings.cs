namespace ErrorProne.NET.StructAnalyzers
{
    public static class Settings
    {
        // Only suggest when the struct is greater or equals to the threashold
        public static readonly int LargeStructThreashold = 2 * sizeof(long);
    }
}
namespace ErrorProne.NET.StructAnalyzers
{
    public static class Settings
    {
        // Only suggest when the struct is greater or equals to the threshold
        public static int LargeStructThreshold { get; private set; } =  3 * sizeof(long);

        /// <summary>
        /// Sets the large struct limit. SHOULD BE USED BY TESTS ONLY!
        /// </summary>
        public static void SetLargeStructThresholdForTestingPurposesOnly(int largeStructSize)
        {
            LargeStructThreshold = largeStructSize;
        }
    }
}
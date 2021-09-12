using Microsoft.CodeAnalysis.Diagnostics;

namespace ErrorProne.NET.StructAnalyzers
{
    public static class Settings
    {
        public static int DefaultLargeStructThreshold { get; private set; } = 5 * sizeof(long);
        private const int MaxLargeStructThreshold = 1_000_000;

        public static void SetDefaultLargeStructThreshold(int newThreshold) =>
            DefaultLargeStructThreshold = newThreshold;

        public static int GetLargeStructThresholdOrDefault(AnalyzerConfigOptions? options)
        {
            if (options is null)
            {
                return DefaultLargeStructThreshold;
            }

            return GetLargeStructThreshold(options);
        }

        public static int GetLargeStructThreshold(AnalyzerConfigOptions options)
        {
            if (!options.TryGetValue("error_prone.large_struct_threshold", out var thresholdString)
                || string.IsNullOrEmpty(thresholdString)
                || thresholdString == "unset")
            {
                return DefaultLargeStructThreshold;
            }

            if (thresholdString.Length > 6)
            {
                // A threshold of 1MB or larger is a configuration error. Return early to ensure the code below will not
                // overflow.
                return MaxLargeStructThreshold;
            }

            // Fast string to int
            var result = 0;
            foreach (var ch in thresholdString)
            {
                if (ch < '0' || ch > '9')
                {
                    return DefaultLargeStructThreshold;
                }

                result = result * 10 + (ch - '0');
            }

            return result;
        }
    }
}
using System.Diagnostics.Contracts;
using System.IO;

namespace ErrorProne.NET.Cli.Utilities
{
    internal static class FileUtilities
    {
        public static void TryDeleteIfNeeded(string path)
        {
            Contract.Requires(!string.IsNullOrEmpty(path));

            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch(IOException) { }
            }
        }
    }
}
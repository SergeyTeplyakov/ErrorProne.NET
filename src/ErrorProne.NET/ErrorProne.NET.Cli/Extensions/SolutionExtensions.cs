using System.Linq;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Cli.Extensions
{
    internal static class SolutionExtensions
    {
        public static int DocumentsCount(this Solution solution)
        {
            return solution.Projects.Sum(p => p.DocumentIds.Count);
        }
    }

}
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace ErrorProne.Samples.CoreAnalyzers
{
    public class UnobservedResults
    {
        public class Result
        {
            public bool Success = false;
        }
        public static Result ProcessRequest() => null;

        public static void UnobserveErrors()
        {
            // The result of type 'Result' should be observed
            ProcessRequest();
        }

        public static void Sampe()
        {
            Stream s = null;
            // The result of type 'Task' should be observed
            s.FlushAsync();

            // The result of type 'Exception' should be observed
            getException();
            Exception getException() => null;

            fooAsync();

            Task fooAsync() => null;

            Result bar() => default;
        }

        public static async Task WhenAllShouldBeFine(Task task1, Task task2)
        {
            await Task.WhenAny(task1, task2);
        }
    }
}
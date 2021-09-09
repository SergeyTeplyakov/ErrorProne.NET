using System.Threading.Tasks;

[System.AttributeUsage(System.AttributeTargets.Struct)]
public class NonDefaultableAttribute : System.Attribute { }


namespace ErrorProne.Samples.CoreAnalyzers
{
    [NonDefaultable]
    public readonly struct TaskSourceSlim<T>
    {
        private readonly TaskCompletionSource<T> _tcs;
        public TaskSourceSlim(TaskCompletionSource<T> tcs) => _tcs = tcs;
        // Other members
    }

    public class DefaultConstructionSample
    {
        public static void Sample()
        {
            // Do not use default construction for struct 'TaskSourceSlim' marked with 'DoNotUseDefaultConstruction' attribute
            var tss = new TaskSourceSlim<object>();
            
            // The same warning.
            TaskSourceSlim<object> tss2 = default;

            // The same warning.
            var tss3 = Create<TaskSourceSlim<object>>();
        }

        public static T Create<T>() where T : new() => default;
    }

    public struct S2
    {
        // Do not embed struct 'TaskSourceSlim' marked with 'DoNotUseDefaultConstruction' attribute into another struct
        private readonly TaskSourceSlim<object> _tss;
    }
}
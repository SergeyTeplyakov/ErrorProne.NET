using System.Threading.Tasks;

[System.AttributeUsage(System.AttributeTargets.Struct)]
public class DoNotUseDefaultConstructionAttribute : System.Attribute { }


namespace ErrorProne.Samples.CoreAnalyzers
{
    [DoNotUseDefaultConstruction]
    public readonly struct TaskSourceSlim<T>
    {
        private readonly TaskCompletionSource<T> _tcs;
        public TaskSourceSlim(TaskCompletionSource<T> tcs) => _tcs = tcs;
        // Other members
    }

    public class DefaultContrcutionSample
    {
        public static void Smple()
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

    public readonly struct S2
    {
        // Do not embed struct 'TaskSourceSlim' marked with 'DoNotUseDefaultConstruction' attribute into another struct
        private readonly TaskSourceSlim<object> _tss;
    }
}
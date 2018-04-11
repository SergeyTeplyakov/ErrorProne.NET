namespace ErrorProne.NET.Structs
{
    /// <summary>
    /// Diagnostitcs produced by this project.
    /// </summary>
    public static class DiagnosticIds
    {
        /// <nodoc />
        public const string MakeStructReadonlyDiagnosticId = "MakeStructReadonlyId";

        /// <nodoc />
        public const string NonReadOnlyStructPassedAsInParameterDiagnosticId = "NonReadOnlyStructPassedAsInParameterId";
        
        /// <nodoc />
        public const string NonReadOnlyStructReturnedByReadOnlyRefDiagnosticId = "NonReadOnlyStructReturnedByReadOnlyRefId";

        /// <nodoc />
        public const string UseInModifierForReadOnlyStructDiagnosticId = "UseInModifierForReadOnlyStructDiagnosticId";

        // Separate 4 cases:
        // 1. Struct is readonly - safe to pass/return by readonly ref
        // 2. Struct is non-readonly but has only exposed fields with no exposed props/methods - safe to pass/return by readonly ref
        // 3. Struct is non-readonly and doesn't have exposed fields - unsafe to pass/return by readonly ref
        // 4. Struct is non-readonly and has exposed fields and props/methods.

        // Readonly ref local of a generic type: not clear what the perf implications are.

        // Show all hidden copies for structs.

        // Calling a generic method with ref return/in-modifier with non-readonly struct.

        // Struct is too big and used in many methods?
        // Struct may be readonly by changing readonly modifers for fields/properties. (like field may be readonly and public int X {get; private set;} the setter is never used but in constructor

        // Move and enhance 'non-pure method' on readonly field?
        // Equals and gethashcode are asymmetrical (different props/fields are used).

    }
}
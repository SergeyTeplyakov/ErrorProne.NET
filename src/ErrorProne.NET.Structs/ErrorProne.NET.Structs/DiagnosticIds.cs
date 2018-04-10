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
        public const string UseInModifierForReadOnlyStructDiagnosticId = "UseInModifierForReadOnlyStructDiagnosticId";

        // Ideas: the struct is readonly and instead of passing by value or ref may be passed by 'in'.

        // Readonly ref local of a generic type: not clear what the perf implications are.

        // Calling a generic method with ref return/in-modifier with non-readonly struct.

        // Struct is too big and used in many methods?
        // Struct may be readonly by changing readonly modifers for fields/properties. (like field may be readonly and public int X {get; private set;} the setter is never used but in constructor

        // Move and enhance 'non-pure method' on readonly field?
        // Equals and gethashcode are asymmetrical (different props/fields are used).

    }
}
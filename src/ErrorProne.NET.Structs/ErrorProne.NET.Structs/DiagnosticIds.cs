namespace ErrorProne.NET.Structs
{
    /// <summary>
    /// Diagnostitcs used by this project.
    /// </summary>
    public static class DiagnosticIds
    {
        /// <nodoc />
        public const string MakeStructReadonlyDiagnosticId = "MakeStructReadonlyId";

        /// <nodoc />
        public const string NonReadOnlyStructPassedAsInParameterDiagnosticId = "NonReadOnlyStructPassedAsInParameterId";

        // Ideas: the struct is readonly and instead of passing by value or ref may be passed by 'in'.
        // Struct is too big and used in many methods?
        // Struct may be readonly by changing readonly modifers for fields/properties. (like field may be readonly and public int X {get; private set;} the setter is never used but in constructor
        
        // Move and enhance 'non-pure method' on readonly field?
        // Equals and gethashcode are asymmetrical (different props/fields are used).

    }
}
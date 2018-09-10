using System;

namespace ErrorProne.NET.Annotations
{
    /// <summary>
    /// Attribute that mimics readonly modifier on fields.
    /// </summary>
    /// <remarks>
    /// The reason of this attribute is to get readonly behavior without paying the cost of
    /// copying readonly fields of value types.
    /// 
    /// ErrorProne.NET will emit an error if this attribute would be applied on the field of non-custom structs.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field)]
    public class ReadOnlyAttribute : Attribute
    {}
}

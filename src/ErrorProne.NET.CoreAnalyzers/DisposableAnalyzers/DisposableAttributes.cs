namespace ErrorProne.NET.DisposableAnalyzers;

public static class DisposableAttributes
{
    // TODO: document here? or in some other place?
    public const string AcquiresOwnershipAttribute = "AcquiresOwnershipAttribute";
    
    // For methods that want to emphasize that the ownership is not transferred.
    public const string KeepsOwnershipAttribute = "KeepsOwnershipAttribute";

    // For properties to emphasize that the ownership is transferred.
    public const string ReleasesOwnershipAttribute = "ReleasesOwnershipAttribute";


    // For fields/properties, whe the ownership belongs to another type.
    public const string NoOwnershipAttribute = "NoOwnershipAttribute";


}
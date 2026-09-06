namespace ErrorProne.NET.DisposableAnalyzers;

public static class DisposableAttributes
{
    public const string DoNotDisposeAttribute = "DoNotDisposeAttribute";

    // Attribute contracts are documented in docs/Rules/ERP044.md.
    public const string AcquiresOwnershipAttribute = "AcquiresOwnershipAttribute";
    
    // For methods that want to emphasize that the ownership is not transferred.
    public const string KeepsOwnershipAttribute = "KeepsOwnershipAttribute";

    // For methods and properties whose results transfer ownership.
    public const string ReturnsOwnershipAttribute = "ReturnsOwnershipAttribute";


    // For borrowed parameters, fields and properties.
    public const string NoOwnershipAttribute = "NoOwnershipAttribute";


}
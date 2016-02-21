namespace ErrorProne.NET.Common
{
    public static class RuleIds
    {
        public const string SideEffectFreeExceptionContructionId = "ERP001";
        public const string UnobservedPureMethodInvocationId = "ERP002";
        public const string AssignmentFreeImmutableObjectContructionId = "ERP003";

        public const string StringFormatInvalidIndexId = "ERP011";
        public const string StringFormatHasEcessiveArgumentId = "ERP012";
        public const string StringFormatInvalidId = "ERP013";

        // Exception handling
        public const string IncorrectExceptionPropagation = "ERP021";
        public const string AllExceptionSwalled = "ERP022";
        public const string InvalidExceptionHandling = "ERP023";
    }
}
namespace ErrorProne.NET.Common
{
    public static class RuleIds
    {
        public const string SideEffectFreeExceptionContructionId = "ERR1";
        public const string UnobservedPureMethodInvocationId = "ERR2";

        public const string StringFormatInvalidIndexId = "ERR4";
        public const string StringFormatHasEcessiveArgumentId = "ERR4";
        public const string StringFormatInvalidId = "ERR5";

        public const string AssignmentFreeObjectContructionId = "WARN1";

        // Exception handling
        public const string IncorrectExceptionPropagation = "EW01";
        public const string AllExceptionSwalled = "EW01";
    }
}
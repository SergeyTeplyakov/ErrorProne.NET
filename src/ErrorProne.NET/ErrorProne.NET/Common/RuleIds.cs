using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ErrorProne.NET.Common
{
    public static class RuleIds
    {
        // 
        public const string UnobservedPureMethodInvocationId = "ERP001";
        public const string SideEffectFreeExceptionContructionId = "ERP002";
        public const string AssignmentFreeImmutableObjectContructionId = "ERP003";
        public const string NonPureMethodsOnReadonlyStructs = "ERP004";

        public const string StringFormatInvalidIndexId = "ERP011";
        public const string StringFormatHasEcessiveArgumentId = "ERP012";
        public const string StringFormatInvalidId = "ERP013";
        public const string RegexPatternIsInvalid = "ERP014";

        // Exception handling
        public const string IncorrectExceptionPropagation = "ERP021";
        public const string AllExceptionSwalled = "ERP022";
        public const string OnlyExceptionMessageWasObserved = "ERP023";

        // Other
        public const string MissingCasesInSwitchStatement = "ERP031";

        public const string ReadonlyPropertyWasNeverAssigned = "ERP032";
        public const string PropertyWithPrivateSetterWasNeverAssigned = "ERP033";
        public const string ReadonlyFieldWasNeverAssigned = "ERP034";
    }
}
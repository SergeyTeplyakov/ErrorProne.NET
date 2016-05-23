﻿using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ErrorProne.NET.Common
{
    public static class RuleIds
    {
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

        // ReadOnly attribute an readonly releated analysis
        public const string ReadonlyPropertyWasNeverAssigned = "ERP031";
        public const string PropertyWithPrivateSetterWasNeverAssigned = "ERP032";
        public const string ReadonlyFieldWasNeverAssigned = "ERP033";
        public const string ReadOnlyFieldWasAssignedOutsideConstructor = "ERP034";
        public const string PrivateFieldWasNeverUsed = "ERP035";

        public const string ReadonlyAttributeNotOnCustomStructs = "ERP041";
        public const string UseReadOnlyAttributeInstead = "ERP042";

        // Async methods
        public const string SuspiciousPreconditionInAsyncMethod = "ERP051";
        public const string SuspiciousPreconditionInIteratorBlock = "ERP052";

        // Other
        public const string MissingCasesInSwitchStatement = "ERP101";
        public const string StringCloneMethodWasUsed = "ERP102";

        // Errors in DebuggerDisplayAttribute
        public const string DebuggerDisplayAttributeInvalidFormat = "ERP103";

        // Argument exception
        public const string ArgumentExceptionParamNameRequired = "ERP061";
        public const string ArgumentExceptionMethodHasNoSuchParamName = "ERP062";
        public const string ArgumentExceptionParamNameShouldNotBeString = "ERP063";
    }
}
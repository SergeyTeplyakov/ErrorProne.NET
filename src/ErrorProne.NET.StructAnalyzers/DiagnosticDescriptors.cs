using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.StructAnalyzers
{
    internal static class DiagnosticDescriptors
    {
        public const string CategoryUsage = "Usage";
        public const string PerformanceCategory = "Performance";
        public const string CodeSmellCategory = "CodeSmell";

        public static readonly DiagnosticDescriptor EPS01 = new DiagnosticDescriptor(
            nameof(EPS01),
            title: "A struct can be made readonly.",
            messageFormat: "A struct '{0}' can be made readonly.",
            category: PerformanceCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            description: "Readonly structs have a better performance when passed or return by readonly reference.");

        public static readonly DiagnosticDescriptor EPS05 =
            new DiagnosticDescriptor(nameof(EPS05), 
                title: "Use in-modifier for a readonly struct.",
                "Use in-modifier for passing a readonly struct '{0}' of estimated size '{1}'.", 
                category: PerformanceCategory, DiagnosticSeverity.Info, isEnabledByDefault: true, 
                description: "Readonly structs have better performance when passed readonly reference.");

        public static readonly DiagnosticDescriptor EPS06 = new DiagnosticDescriptor(
            nameof(EPS06), 
            title: "An operation causes a hidden struct copy.", 
            messageFormat: "An expression '{0}' causes a hidden copy of a {2}struct '{1}' of estimated size '{3}'.", 
            category: PerformanceCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
            description: "The compiler emits a defensive copy to make sure a struct instance remains unchanged.");

        public static readonly DiagnosticDescriptor EPS07 = new DiagnosticDescriptor(
            nameof(EPS07), 
            "A hash table \"unfriendly\" type is used as the key in a hash table.", 
            "A struct '{0}' with a default {1} implementation is used as a key in a hash table.",
            category: PerformanceCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
            description: "The default implementation of 'Equals' and 'GetHashCode' for structs is inefficient and could cause severe performance issues.");

        public static readonly DiagnosticDescriptor EPS08 =
            new DiagnosticDescriptor(nameof(EPS08), 
                title: "Default 'ValueType.Equals' or 'HashCode' is used for struct equality.", 
                messageFormat: "The default 'ValueType.{0}' is used in {1}.", 
                category: PerformanceCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
                description: "The default implementation of 'Equals' and 'GetHashCode' for structs is inefficient and could cause severe performance issues.");

        public static readonly DiagnosticDescriptor EPS09 = new DiagnosticDescriptor(
            nameof(EPS09), 
            title: "Pass an argument for an 'in' parameter explicitly.", 
            messageFormat: "An argument for a parameter '{0}' may be passed explicitly.", 
            category: CategoryUsage, DiagnosticSeverity.Info, isEnabledByDefault: true, 
            description: "Pass an argument for an 'in' parameters explicitly.");

        public static readonly DiagnosticDescriptor EPS10 = new DiagnosticDescriptor(
            nameof(EPS10), "Do not construct non-defaultable struct with 'default' expression.", 
            "A non-defaultable struct '{0}' should not be created with a 'default' expression.{1}", 
            category: CodeSmellCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
            description: "Non-defaultable structs can not be constructed using the default constructor or default(T) expression because of their invariants.");

        /// <nodoc />
        public static readonly DiagnosticDescriptor EPS11 = new DiagnosticDescriptor(
            nameof(EPS11), 
            title: "Do not embed non-defaultable structs into another structs.",
            messageFormat: "A non-defaultable struct '{0}' is embedded in a normal struct.", 
            category: CodeSmellCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
            description: "Non-defaultable structs should be " +
                         "constructed using a non-default constructor and can not be embedded " +
                         "in other defaultable structs .");

        public static readonly DiagnosticDescriptor EPS12 = new DiagnosticDescriptor(
            nameof(EPS12), 
            title: "A struct member can be made readonly.", 
            messageFormat: "A {0} can be made readonly for struct {1} of size {2}.", 
            category: PerformanceCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
            description: "Readonly struct members are more efficient in readonly context by avoiding hidden copies.");

        public static readonly DiagnosticDescriptor EPS13 = new DiagnosticDescriptor(
            nameof(EPS13),
            title: "A non-defaultable struct must declare a constructor.",
            messageFormat: "A non-defaultable struct {0} must declare a constructor.",
            category: CategoryUsage, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            description: "A non-defaultable struct must be created with a constructor so you must declare one in order to use it.");
    }
}
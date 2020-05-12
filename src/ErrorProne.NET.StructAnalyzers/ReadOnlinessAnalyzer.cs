using System.Linq;
using ErrorProne.NET.Core;
using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.StructAnalyzers
{
    /// <summary>
    /// Contains different types of friendliness in respect to readonly references.
    /// </summary>
    public enum ReadOnlyRefFriendliness
    {
        /// <summary>
        /// The type is of reference type and is ref-readonly friendly by definition.
        /// </summary>
        FriendlyClass,

        /// <summary>
        /// A struct can be used in readonly ref contexts without any performance penalties because the struct is readonly.
        /// </summary>
        FriendlyReadOnlyStruct,

        /// <summary>
        /// A struct can be used in readonly ref contexts because the struct has fields only and doesn't have props or methods.
        /// </summary>
        FriendlyPoco,

        /// <summary>
        /// A should not be used in readonly ref contexts because it consists of properties and methods,
        /// that will definitely cause a defensive copy every time it will be used.
        /// </summary>
        Unfriendly,

        /// <summary>
        /// A struct has fields and methods/properties, so it is impossible to decide immediately whether its safe to use it
        /// in readonly ref contexts or not. Further analysis is required (based on how variable is used).
        /// </summary>
        Unknown,
    }

    /// <summary>
    /// Provides some insights regarding structs: is it safe to pass/return them by readonly reference or not.
    /// </summary>
    public static class ReadOnlinessAnalyzer
    {
        /// <nodoc />
        public static bool UnfriendlyToReadOnlyRefs(this ITypeSymbol type)
            => type.AnalyzeReadOnlyFriendliness() == ReadOnlyRefFriendliness.Unfriendly;

        /// <summary>
        /// Returns true if a given <paramref name="type"/> has at least one instance field.
        /// </summary>
        public static bool HasInstanceFields(this ITypeSymbol type)
        {
            return type.GetMembers().Where(m => !m.IsStatic).Any(m => m is IFieldSymbol fs && !fs.IsImplicitlyDeclared);
        }

        /// <nodoc />
        public static ReadOnlyRefFriendliness AnalyzeReadOnlyFriendliness(this ITypeSymbol type)
        {
            if (!type.IsValueType)
            {
                return ReadOnlyRefFriendliness.FriendlyClass;
            }

            if (type.IsReadOnlyStruct())
            {
                return ReadOnlyRefFriendliness.FriendlyReadOnlyStruct;
            }

            bool hasFields = false;
            bool hasPropertiesOrMethods = false;

            foreach (var member in type.GetMembers())
            {
                // Should ignore static members, they has nothing to do with it.
                if (member.IsStatic)
                {
                    continue;
                }

                switch (member)
                {
                    case IFieldSymbol fs when fs.DeclaredAccessibility != Accessibility.Private:
                        hasFields = true;
                        break;
                    case IPropertySymbol ps when ps.DeclaredAccessibility != Accessibility.Private:
                    case IMethodSymbol ms when ms.DeclaredAccessibility != Accessibility.Private 
                                               && ms.MethodKind != MethodKind.Constructor:
                        hasPropertiesOrMethods = true;
                        break;
                }
            }

            if (!hasPropertiesOrMethods)
            {
                // No methods/properties: POCO or empty
                return ReadOnlyRefFriendliness.FriendlyPoco;
            }

            if (!hasFields)
            {
                // Only properties or methods: unfriendly
                return ReadOnlyRefFriendliness.Unfriendly;
            }

            // A struct has both, accessible fields and properties or methods
            return ReadOnlyRefFriendliness.Unknown;
        }
    }
}
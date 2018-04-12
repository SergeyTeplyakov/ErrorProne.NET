using Microsoft.CodeAnalysis;

namespace ErrorProne.NET.Structs
{
    /// <nodoc />
    public static class SymbolExtensions
    {
        /// <summary>
        /// Returns true if a given <paramref name="symbol"/> is an implementation of an interface member.
        /// </summary>
        public static bool IsInterfaceImplementation(this ISymbol symbol)
        {
            if (symbol.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }

            var containingType = symbol.ContainingType;
            var implementedInterfaces = containingType.AllInterfaces;

            foreach (var implementedInterface in implementedInterfaces)
            {
                var implementedInterfaceMembersWithSameName = implementedInterface.GetMembers(symbol.Name);
                foreach (var implementedInterfaceMember in implementedInterfaceMembersWithSameName)
                {
                    if (symbol.Equals(containingType.FindImplementationForInterfaceMember(implementedInterfaceMember)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
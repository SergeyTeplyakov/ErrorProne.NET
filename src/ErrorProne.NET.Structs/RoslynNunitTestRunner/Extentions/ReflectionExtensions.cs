using System;
using System.Reflection;

namespace RoslynNunitTestRunner.Reflection
{
    internal static class ReflectionExtensions
    {
        private static class AssemblyLightUp
        {
            internal static readonly Type Type = typeof(Assembly);

            internal static readonly Func<Assembly, string> get_Location = Type
                .GetTypeInfo()
                .GetDeclaredMethod("get_Location")
                .CreateDelegate<Func<Assembly, string>>();
        }

        public static string GetLocation(this Assembly assembly)
        {
            if (AssemblyLightUp.get_Location == null)
            {
                throw new PlatformNotSupportedException();
            }

            return AssemblyLightUp.get_Location(assembly);
        }

        public static T CreateDelegate<T>(this MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                return default(T);
            }

            return (T)(object)methodInfo.CreateDelegate(typeof(T));
        }
    }
}

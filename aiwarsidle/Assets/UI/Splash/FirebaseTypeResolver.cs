using System;
using System.Reflection;

namespace AIWarsIdle.UI.Splash
{
    internal static class FirebaseTypeResolver
    {
        public static Type GetTypeOrNull(string fullName, string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return null;

            if (!string.IsNullOrWhiteSpace(assemblyName))
            {
                var t = Type.GetType($"{fullName}, {assemblyName}");
                if (t != null) return t;
            }

            // Fallback: search loaded assemblies (more robust across runtimes).
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var a = assemblies[i];
                if (a == null) continue;

                if (!string.IsNullOrWhiteSpace(assemblyName))
                {
                    var name = a.GetName().Name;
                    if (!string.Equals(name, assemblyName, StringComparison.Ordinal)) continue;
                }

                var resolved = a.GetType(fullName, throwOnError: false);
                if (resolved != null) return resolved;
            }

            return null;
        }
    }
}


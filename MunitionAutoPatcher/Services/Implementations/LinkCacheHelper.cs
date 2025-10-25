using System.Reflection;
using System.Linq;

namespace MunitionAutoPatcher.Services.Implementations
{
    // Small helper to expose TryResolve semantics via reflection in a stable place
    public static class LinkCacheHelper
    {
        // Attempts to call LinkCache.TryResolve(linkLike, out resolved) via reflection.
        // Returns the resolved object or null if resolution failed / not available.
        public static object? TryResolveViaLinkCache(object? linkLike, object? linkCache)
        {
            if (linkLike == null || linkCache == null) return null;
            try
            {
                var lcType = linkCache.GetType();
                var methods = lcType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == "TryResolve" && m.GetParameters().Length == 2);
                foreach (var m in methods)
                {
                    try
                    {
                        MethodInfo invokeMethod = m;

                        // If the method is generic (e.g. TryResolve<T>), try to construct a closed generic using the generic argument
                        if (m.IsGenericMethodDefinition)
                        {
                            // Attempt to infer the generic type argument from linkLike (e.g. FormLink<T>)
                            var linkType = linkLike.GetType();
                            if (linkType.IsGenericType)
                            {
                                var genArg = linkType.GetGenericArguments().FirstOrDefault();
                                if (genArg != null)
                                {
                                    try { invokeMethod = m.MakeGenericMethod(genArg); } catch { continue; }
                                }
                                else continue;
                            }
                            else continue;
                        }

                        var p0 = invokeMethod.GetParameters()[0].ParameterType;
                        var linkTypeActual = linkLike.GetType();

                        // Try multiple compatibility checks for the first parameter type
                        bool compatible = p0 == linkTypeActual || p0.IsAssignableFrom(linkTypeActual) || linkTypeActual.IsAssignableFrom(p0);
                        if (!compatible)
                            continue;

                        // Prepare args array for invocation: [linkLike, out resolved]
                        var args = new object?[] { linkLike, null };
                        var okObj = invokeMethod.Invoke(linkCache, args);
                        var ok = okObj as bool? ?? (okObj is bool b && b);
                        if (ok)
                        {
                            return args[1];
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }
    }
}

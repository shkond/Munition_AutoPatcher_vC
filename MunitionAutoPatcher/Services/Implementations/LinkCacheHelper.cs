using System.Reflection;

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
                        var p0 = m.GetParameters()[0].ParameterType;
                        if (!p0.IsAssignableFrom(linkLike.GetType())) continue;
                        var args = new object?[] { linkLike, null };
                        var ok = (bool?)m.Invoke(linkCache, args);
                        if (ok == true)
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

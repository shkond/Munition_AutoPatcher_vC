using System.Reflection;
using System.Linq;
using System.Collections.Concurrent;
using System.Diagnostics;

// Combined resolver: tries instance TryResolve on the link-like object, then FormKey->Resolve/TryResolve,
// then falls back to LinkCache.TryResolve variants (including generic TryResolve<T>) with caching.

namespace MunitionAutoPatcher.Services.Implementations
{
    // Small helper to expose TryResolve semantics via reflection in a stable place
    public static class LinkCacheHelper
    {
        private static readonly ConcurrentDictionary<Type, MethodInfo?> s_instanceTryResolve = new();
        private static readonly ConcurrentDictionary<Type, PropertyInfo?> s_formKeyProp = new();
        private static readonly ConcurrentDictionary<Type, MethodInfo?[]> s_linkCacheTryResolveMethods = new();
        private static readonly ConcurrentDictionary<Type, MethodInfo?> s_linkCacheResolveByKey = new();

        // Attempts multiple strategies to resolve a link-like value against a Mutagen LinkCache.
        // Returns the resolved object or null if not resolved.
        public static object? TryResolveViaLinkCache(object? linkLike, object? linkCache)
        {
            if (linkLike == null || linkCache == null) return null;

            // 1) Prefer calling an instance-level TryResolve on the link-like object: linkLike.TryResolve(linkCache, out resolved)
            try
            {
                var t = linkLike.GetType();
                var inst = s_instanceTryResolve.GetOrAdd(t, tt =>
                    tt.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                      .FirstOrDefault(m => string.Equals(m.Name, "TryResolve", StringComparison.Ordinal) && m.GetParameters().Length == 2)
                );

                if (inst != null)
                {
                    try
                    {
                        var args = new object?[] { linkCache, null };
                        var okObj = inst.Invoke(linkLike, args);
                        if (okObj is bool ok && ok && args[1] != null)
                            return args[1];
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"LinkCacheHelper: instance TryResolve invocation failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LinkCacheHelper: instance TryResolve discovery failed: {ex.Message}");
            }

            // 2) Try to extract a FormKey-like property and use linkCache.Resolve(formKey) or linkCache.TryResolve(formKey, out resolved)
            object? formKeyObj = null;
            try
            {
                var fk = s_formKeyProp.GetOrAdd(linkLike.GetType(), tt => tt.GetProperty("FormKey", BindingFlags.Public | BindingFlags.Instance));
                if (fk != null)
                    formKeyObj = fk.GetValue(linkLike);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LinkCacheHelper: FormKey extraction failed: {ex.Message}");
                formKeyObj = null;
            }

            if (formKeyObj != null)
            {
                try
                {
                    var lcType = linkCache.GetType();

                    // 2a) Try linkCache.Resolve(formKey) -> returns resolved directly
                    var resolve = s_linkCacheResolveByKey.GetOrAdd(lcType, lc =>
                        lc.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                          .FirstOrDefault(m => string.Equals(m.Name, "Resolve", StringComparison.Ordinal) && m.GetParameters().Length == 1)
                    );
                    if (resolve != null)
                    {
                        try
                        {
                            var rr = resolve.Invoke(linkCache, new object?[] { formKeyObj });
                            if (rr != null) return rr;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"LinkCacheHelper: linkCache.Resolve(formKey) invocation failed: {ex.Message}");
                            /* ignore and try TryResolve */
                        }
                    }

                    // 2b) Try linkCache.TryResolve(formKey, out resolved)
                    var tryMethods = s_linkCacheTryResolveMethods.GetOrAdd(lcType, lc =>
                        lc.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                          .Where(m => string.Equals(m.Name, "TryResolve", StringComparison.Ordinal) && m.GetParameters().Length == 2)
                          .ToArray()
                    );

                    foreach (var m in tryMethods)
                    {
                        try
                        {
                            var invoke = m;
                            if (m.IsGenericMethodDefinition)
                            {
                                // attempt to close generic on the formKey object's type if possible
                                var genArg = formKeyObj.GetType();
                                try { invoke = m.MakeGenericMethod(genArg); } catch (Exception ex) { Debug.WriteLine($"LinkCacheHelper: MakeGenericMethod failed for TryResolve(formKey): {ex.Message}"); continue; }
                            }

                            var p0 = invoke.GetParameters()[0].ParameterType;
                            if (!p0.IsAssignableFrom(formKeyObj.GetType()) && p0 != typeof(object))
                                continue;

                            var args = new object?[] { formKeyObj, null };
                            var okObj = invoke.Invoke(linkCache, args);
                            if (okObj is bool ok && ok && args[1] != null)
                                return args[1];
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"LinkCacheHelper: TryResolve(formKey) candidate invocation failed: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"LinkCacheHelper: FormKey-based resolution failed: {ex.Message}");
                }
            }

            // 3) Fallback: try LinkCache.TryResolve(linkLike, out resolved) variants (including generic TryResolve<T>)
            try
            {
                var lcType = linkCache.GetType();
                var tryMethods = s_linkCacheTryResolveMethods.GetOrAdd(lcType, lc =>
                    lc.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                      .Where(m => string.Equals(m.Name, "TryResolve", StringComparison.Ordinal) && m.GetParameters().Length == 2)
                      .ToArray()
                );

                foreach (var m in tryMethods)
                {
                    try
                    {
                        MethodInfo invoke = m;
                        if (m.IsGenericMethodDefinition)
                        {
                            var linkType = linkLike.GetType();
                            if (linkType.IsGenericType)
                            {
                                var genArg = linkType.GetGenericArguments().FirstOrDefault();
                                if (genArg != null)
                                {
                                    try { invoke = m.MakeGenericMethod(genArg); } catch (Exception ex) { Debug.WriteLine($"LinkCacheHelper: MakeGenericMethod failed for TryResolve(linkLike): {ex.Message}"); continue; }
                                }
                                else continue;
                            }
                            else continue;
                        }

                        var p0 = invoke.GetParameters()[0].ParameterType;
                        var linkTypeActual = linkLike.GetType();

                        bool compatible = p0 == linkTypeActual || p0.IsAssignableFrom(linkTypeActual) || linkTypeActual.IsAssignableFrom(p0);
                        if (!compatible) continue;

                        var args = new object?[] { linkLike, null };
                        var okObj = invoke.Invoke(linkCache, args);
                        var ok = okObj as bool? ?? (okObj is bool b && b);
                        if (ok)
                            return args[1];
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"LinkCacheHelper: TryResolve(linkLike) candidate invocation failed: {ex.Message}");
                    }
                }
            }
            catch { }

            // 4) As a last resort: try any public instance method on linkCache that accepts a single parameter compatible with linkLike or formKeyObj
            try
            {
                var lcType = linkCache.GetType();
                var candidates = lcType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetParameters().Length == 1)
                    .ToArray();

                foreach (var m in candidates)
                {
                    try
                    {
                        var pType = m.GetParameters()[0].ParameterType;
                        var arg = formKeyObj ?? linkLike;
                        if (arg == null) continue;
                        if (!pType.IsAssignableFrom(arg.GetType()) && pType != typeof(object))
                            continue;
                        var r = m.Invoke(linkCache, new object?[] { arg });
                        if (r != null) return r;
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        // Small helper: guess whether resolved getter is ammo/projectile by name/interface heuristics
        public static bool IsAmmoOrProjectile(object? getter)
        {
            if (getter == null) return false;
            var t = getter.GetType();
            var name = t.Name ?? string.Empty;
            var lname = name.ToLowerInvariant();
            if (lname.Contains("ammo") || lname.Contains("projectile") || lname.Contains("bullet"))
                return true;
            return false;
        }
    }
}

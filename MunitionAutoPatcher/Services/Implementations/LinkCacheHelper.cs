using System.Reflection;
using System.Linq;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows;

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
        private static readonly System.Threading.AsyncLocal<HashSet<string>?> s_currentResolutionKeys = new();

        // Attempts multiple strategies to resolve a link-like value against a Mutagen LinkCache.
        // Returns the resolved object or null if not resolved.
        public static object? TryResolveViaLinkCache(object? linkLike, object? linkCache)
        {
            // Guard against null inputs: callers may pass null when a given link isn't present.
            if (linkLike == null || linkCache == null)
                return null;

            var key = $"{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(linkLike)}|{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(linkCache)}|{linkLike.GetType().FullName}|{linkCache.GetType().FullName}";
            var set = s_currentResolutionKeys.Value ??= new HashSet<string>();
            if (!set.Add(key))
            {
                AppLogger.Log($"LinkCacheHelper: re-entrant resolution detected for key {key}; aborting to avoid cycle.");
                return null;
            }

            try
            {
                // 1) インスタンス側の TryResolve を優先
                var instResolved = TryInstanceResolve(linkLike, linkCache);
                if (instResolved != null) return instResolved;

                // 2) FormKey ベースで解決
                var formKeyObj = ExtractFormKey(linkLike);
                if (formKeyObj != null)
                {
                    var byKey = TryFormKeyResolve(formKeyObj, linkCache);
                    if (byKey != null) return byKey;
                }

                // 3) linkLike 自体を TryResolve
                var byLink = TryLinkLikeResolve(linkLike, linkCache);
                if (byLink != null) return byLink;

                // 4) 最終フォールバック: 単一引数の公開メソッド
                var last = TrySingleArgFallback(linkLike, formKeyObj, linkCache);
                if (last != null) return last;

                return null;
            }
            finally
            {
                set.Remove(key);
                if (set.Count == 0) s_currentResolutionKeys.Value = null;
            }
        }

        private static object? TryInstanceResolve(object linkLike, object linkCache)
        {
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
                        AppLogger.Log($"LinkCacheHelper: instance TryResolve invocation failed: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log($"LinkCacheHelper: instance TryResolve discovery failed: {ex.Message}", ex);
            }
            return null;
        }

        private static object? ExtractFormKey(object linkLike)
        {
            try
            {
                var fk = s_formKeyProp.GetOrAdd(linkLike.GetType(), tt => tt.GetProperty("FormKey", BindingFlags.Public | BindingFlags.Instance));
                if (fk != null)
                {
                    var v = fk.GetValue(linkLike);
                    if (v != null) return v;
                }

                try { MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPropertyValue<object>(linkLike, "FormKey", out var formKeyObj); return formKeyObj; }
                catch (Exception ex) { AppLogger.Log($"LinkCacheHelper: helper-based FormKey extraction failed: {ex.Message}", ex); return null; }
            }
            catch (Exception ex)
            {
                AppLogger.Log($"LinkCacheHelper: FormKey extraction failed: {ex.Message}", ex);
                return null;
            }
        }

        private static object? TryFormKeyResolve(object formKeyObj, object linkCache)
        {
            try
            {
                // 1. Convert the formKeyObj to a string representation.
                string? keyString = null;
                try
                {
                    if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPropertyValue<object>(formKeyObj, "ModKey", out var modKey) && modKey != null &&
                        MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPropertyValue<object>(modKey, "FileName", out var fileName) && fileName is string fn &&
                        MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPropertyValue<uint>(formKeyObj, "ID", out var id))
                    {
                        keyString = $"{fn}:{id:X8}";
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Log($"LinkCacheHelper: Failed to construct string key from FormKey object: {ex.Message}", ex);
                    return null;
                }

                var lcType = linkCache.GetType();

                // 2. Find a Resolve(string) method on the link cache and try it first if we have a string key.
                if (!string.IsNullOrEmpty(keyString))
                {
                    var resolve = s_linkCacheResolveByKey.GetOrAdd(lcType, lc =>
                        lc.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                          .FirstOrDefault(m =>
                              string.Equals(m.Name, "Resolve", StringComparison.Ordinal) &&
                              m.GetParameters().Length == 1 &&
                              m.GetParameters()[0].ParameterType == typeof(string))
                    );

                    if (resolve != null)
                    {
                        try
                        {
                            var rr = resolve.Invoke(linkCache, new object?[] { keyString });
                            if (rr != null) return rr;
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Log($"LinkCacheHelper: linkCache.Resolve(string key) invocation failed: {ex.Message}", ex);
                        }
                    }
                }

                // 3. If Resolve(string) didn't return anything (or we couldn't form a string key), try TryResolve(formKey, out object) variants.
                try
                {
                    var tryMethods = s_linkCacheTryResolveMethods.GetOrAdd(lcType, lc =>
                        lc.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                          .Where(m => string.Equals(m.Name, "TryResolve", StringComparison.Ordinal) && m.GetParameters().Length == 2)
                          .ToArray()
                    );

                    foreach (var m in tryMethods)
                    {
                        try
                        {
                            MethodInfo invoke = m!;
                            if (m!.IsGenericMethodDefinition)
                            {
                                var fkType = formKeyObj.GetType();
                                if (!fkType.IsGenericType)
                                    continue;
                                var genArg = fkType.GetGenericArguments().FirstOrDefault();
                                if (genArg == null) continue;
                                try { invoke = m.MakeGenericMethod(genArg); } catch (Exception ex) { AppLogger.Log($"LinkCacheHelper: MakeGenericMethod failed for TryResolve(formKey): {ex.Message}", ex); continue; }
                            }

                            var p0 = invoke.GetParameters()![0]!.ParameterType;
                            var fkActual = formKeyObj.GetType();
                            bool compatible = p0 == fkActual || p0.IsAssignableFrom(fkActual) || fkActual.IsAssignableFrom(p0);
                            if (!compatible) continue;

                            var args = new object?[] { formKeyObj, null };
                            var okObj = invoke.Invoke(linkCache, args);
                            if (okObj is bool ok && ok && args[1] != null)
                                return args[1];
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Log($"LinkCacheHelper: TryResolve(formKey) candidate invocation failed: {ex.Message}", ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Log($"LinkCacheHelper: error while scanning TryResolve(formKey) candidates: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log($"LinkCacheHelper: FormKey-based resolution failed: {ex.Message}", ex);
            }
            return null;
        }

        private static object? TryLinkLikeResolve(object linkLike, object linkCache)
        {
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
                        MethodInfo invoke = m!;
                        if (m!.IsGenericMethodDefinition)
                        {
                            var linkType = linkLike.GetType();
                            if (!linkType.IsGenericType) continue;
                            var genArg = linkType.GetGenericArguments().FirstOrDefault();
                            if (genArg == null) continue;
                            try { invoke = m.MakeGenericMethod(genArg); } catch (Exception ex) { AppLogger.Log($"LinkCacheHelper: MakeGenericMethod failed for TryResolve(linkLike): {ex.Message}", ex); continue; }
                        }

                        var p0 = invoke.GetParameters()![0]!.ParameterType;
                        var linkTypeActual = linkLike.GetType();
                        bool compatible = p0 == linkTypeActual || p0.IsAssignableFrom(linkTypeActual) || linkTypeActual.IsAssignableFrom(p0);
                        if (!compatible) continue;

                        var args = new object?[] { linkLike, null };
                        var okObj = invoke.Invoke(linkCache, args);
                        var ok = okObj as bool? ?? (okObj is bool b && b);
                        if (ok) return args[1];
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Log($"LinkCacheHelper: TryResolve(linkLike) candidate invocation failed: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log("LinkCacheHelper: unexpected error in TryResolve fallback loop", ex);
            }
            return null;
        }

        private static object? TrySingleArgFallback(object? linkLike, object? formKeyObj, object linkCache)
        {
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
                        if (r == null) continue;
                        if (r is bool) continue; // ignore sentinel bools like Equals
                        return r;
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Log($"LinkCacheHelper: fallback single-arg method invocation failed: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log("LinkCacheHelper: unexpected error in fallback single-arg discovery", ex);
            }
            return null;
        }

        // Small helper: guess whether resolved getter is ammo/projectile by name/interface heuristics
        public static bool IsAmmoOrProjectile(object? getter)
        {
            if (getter == null) return false;
            // 利用可能なら型/署名ベースで判定し、最後に名前ヒューリスティクへフォールバック
            try { if (Utilities.MutagenTypeGuards.IsAmmoOrProjectile(getter)) return true; }
            catch { /* fall back below */ }

            var t = getter.GetType();
            var name = t.Name ?? string.Empty;
            var lname = name.ToLowerInvariant();
            return lname.Contains("ammo") || lname.Contains("projectile") || lname.Contains("bullet");
        }

        // LinkCacheHelper previously had an internal Log helper; use shared AppLogger instead.
    }
}

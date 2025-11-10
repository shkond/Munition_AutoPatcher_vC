using System.Reflection;
using System.Linq;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using Mutagen.Bethesda.Plugins; // For FormKey
using Mutagen.Bethesda.Plugins.Records; // For IMajorRecordGetter

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
        private static readonly ConcurrentDictionary<string, int> s_errorCounts = new();
        private const int s_errorSuppressThreshold = 3;
        private static int s_singleArgIncompatLogged = 0;

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
                System.Diagnostics.Debug.WriteLine($"LinkCacheHelper: re-entrant resolution detected for key {key}; aborting to avoid cycle.");
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

                // 4) 3-parameter typed TryResolve(FormKey, Type, out IMajorRecordGetter)
                if (formKeyObj != null)
                {
                    var typed = TryThreeParamFormKeyResolve(formKeyObj, linkCache);
                    if (typed != null) return typed;
                }

                // 4b) Try generic TryResolve<T>(FormKey, out T) variants
                if (formKeyObj != null)
                {
                    var genResolved = TryGenericTryResolve(formKeyObj, linkCache);
                    if (genResolved != null) return genResolved;
                }

                // 5) 最終フォールバック: 単一引数の公開メソッド
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
                        if (inst.ContainsGenericParameters) // avoid invoking open generic instance methods
                        {
                            System.Diagnostics.Debug.WriteLine("LinkCacheHelper: skipping instance TryResolve - method contains generic parameters");
                        }
                        else
                        {
                            var args = new object?[] { linkCache, null };
                            var okObj = inst.Invoke(linkLike, args);
                            if (okObj is bool ok && ok && args[1] != null)
                                return args[1];
                        }
                    }
                    catch (TargetInvocationException tex)
                    {
                        var inner = tex.InnerException?.ToString() ?? tex.ToString();
                        LogErrorOnce("instance_tryresolve_targetinvocation", $"LinkCacheHelper: instance TryResolve invocation TargetInvocationException: {inner}", tex);
                    }
                    catch (Exception ex)
                    {
                        LogErrorOnce("instance_tryresolve", $"LinkCacheHelper: instance TryResolve invocation failed: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LinkCacheHelper: instance TryResolve discovery failed: {ex}");
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
                catch (Exception ex) { LogErrorOnce("extract_formkey_helper_failed", $"LinkCacheHelper: helper-based FormKey extraction failed: {ex.Message}", ex); return null; }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LinkCacheHelper: FormKey extraction failed: {ex}");
                return null;
            }
        }

        private static object? TryFormKeyResolve(object formKeyObj, object linkCache)
        {
            try
            {
                // 1. Convert the formKeyObj to a string representation using the centralized helper.
                string? keyString = null;
                try
                {
                    if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(formKeyObj, out var plugin, out var id))
                    {
                        // Only treat as a valid identifier if plugin is non-empty and id is non-zero.
                        if (!string.IsNullOrWhiteSpace(plugin) && id != 0u)
                        {
                            keyString = $"{plugin}:{id:X8}";
                        }
                        else
                        {
                            LogErrorOnce("formkey_invalid_pluginid", $"LinkCacheHelper: FormKey has empty plugin or zero id - plugin='{plugin}', id={id:X8}");
                        }
                    }
                    else
                    {
                        // Fallback: try the older manual property extraction as a last resort
                        if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPropertyValue<object>(formKeyObj, "ModKey", out var modKey) && modKey != null &&
                            MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPropertyValue<object>(modKey, "FileName", out var fileName) && fileName is string fn &&
                            MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPropertyValue<uint>(formKeyObj, "ID", out var id2))
                        {
                            keyString = $"{fn}:{id2:X8}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogErrorOnce("formkey_string_construct_failed", $"LinkCacheHelper: Failed to construct string key from FormKey object: {ex.Message}", ex);
                    // continue; we'll attempt other resolve strategies
                }

                var lcType = linkCache.GetType();

                // 2. Disabled: Do not call Resolve(string) for FormKey-derived identifiers (expects EditorID).
                // Intentionally skip attempting linkCache.Resolve(string) with plugin:ID or ToString()-based values.

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
                                try { invoke = m.MakeGenericMethod(genArg); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LinkCacheHelper: MakeGenericMethod failed for TryResolve(formKey): {ex}"); continue; }
                            }

                            var paramInfos = invoke.GetParameters();
                            if (paramInfos == null || paramInfos.Length < 1 || paramInfos[0] == null)
                                continue;
                            var p0 = paramInfos[0].ParameterType;
                            var fkActual = formKeyObj.GetType();
                            bool compatible = p0 == fkActual || p0.IsAssignableFrom(fkActual) || fkActual.IsAssignableFrom(p0);
                            if (!compatible)
                            {
                                // Suppress expected noise when method expects string
                                if (p0 != typeof(string))
                                {
                                    try
                                    {
                                        LogErrorOnce("tryresolve_formkey_typemismatch", $"LinkCacheHelper: TryResolve(formKey) type mismatch - LinkCache={linkCache.GetType().FullName}, MethodParam={p0.FullName}, FormKeyType={fkActual.FullName}");
                                    }
                                    catch { }
                                }

                                // If the method expects a string, skip string-based fallback for FormKey.
                                if (p0 == typeof(string))
                                {
                                    // Avoid calling Resolve(string) with FormKey-derived identifiers (expects EditorID).
                                    continue;
                                }

                                continue;
                            }

                            var args = new object?[] { formKeyObj, null };
                            var okObj = invoke.Invoke(linkCache, args);
                            if (okObj is bool ok && ok && args[1] != null)
                                return args[1];
                        }
                        catch (Exception ex)
                        {
                            LogErrorOnce("tryresolve_formkey_candidate_failed", $"LinkCacheHelper: TryResolve(formKey) candidate invocation failed: {ex.Message}", ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogErrorOnce("tryresolve_formkey_scan_failed", $"LinkCacheHelper: error while scanning TryResolve(formkey) candidates: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                LogErrorOnce("formkey_based_resolution_failed", $"LinkCacheHelper: FormKey-based resolution failed: {ex.Message}", ex);
            }
            return null;
        }

        // New typed path: scan for bool TryResolve(FormKey key, Type recordType, out IMajorRecordGetter result)
        private static object? TryThreeParamFormKeyResolve(object formKeyObj, object linkCache)
        {
            try
            {
                var lcType = linkCache.GetType();
                var methods = lcType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => string.Equals(m.Name, "TryResolve", StringComparison.Ordinal) && m.GetParameters().Length == 3)
                    .ToArray();
                if (methods.Length == 0) return null;

                // Ensure we have a Mutagen FormKey; attempt conversion if needed
                object? mfkObj = formKeyObj;
                if (mfkObj is not FormKey)
                {
                    // Try extracting a FormKey via reflection helper
                    try
                    {
                        if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetFormKey(formKeyObj, out var extracted) && extracted is FormKey fkStruct)
                            mfkObj = fkStruct;
                    }
                    catch { mfkObj = null; }
                }
                if (mfkObj is not FormKey) return null;
                var mfk = (FormKey)mfkObj;

                foreach (var m in methods)
                {
                    try
                    {
                        var ps = m.GetParameters();
                        // Validate parameter types
                        if (ps[0].ParameterType != typeof(FormKey)) continue;
                        if (ps[1].ParameterType != typeof(Type)) continue;
                        if (!ps[2].IsOut) continue;

                        var args = new object?[] { mfk, typeof(IMajorRecordGetter), null };
                        var okObj = m.Invoke(linkCache, args);
                        if (okObj is bool ok && ok && args[2] != null)
                            return args[2];
                    }
                    catch (TargetInvocationException tex)
                    {
                        var innerMsg = tex.InnerException?.Message ?? tex.Message;
                        LogErrorOnce("tryresolve_3param_targetinv", $"LinkCacheHelper: 3-param TryResolve invocation failed: {innerMsg}", tex);
                    }
                    catch (Exception ex)
                    {
                        LogErrorOnce("tryresolve_3param_failed", $"LinkCacheHelper: 3-param TryResolve failed: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                LogErrorOnce("tryresolve_3param_scan_failed", $"LinkCacheHelper: 3-param TryResolve scan error: {ex.Message}", ex);
            }
            return null;
        }

        // Attempt to invoke generic TryResolve<T>(FormKey, out T) methods via reflection.
        // This scans loaded assemblies for candidate getter types and tries to close
        // generic TryResolve definitions against them, returning the resolved object
        // when successful.
        private static object? TryGenericTryResolve(object formKeyObj, object linkCache)
        {
            try
            {
                // Ensure we have a Mutagen FormKey
                object? mfkObj = formKeyObj;
                if (mfkObj is not FormKey)
                {
                    try
                    {
                        if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetFormKey(formKeyObj, out var extracted) && extracted is FormKey fkStruct)
                            mfkObj = fkStruct;
                    }
                    catch { mfkObj = null; }
                }
                if (mfkObj is not FormKey) return null;
                var mfk = (FormKey)mfkObj;

                var lcType = linkCache.GetType();
                var genericDefs = lcType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.IsGenericMethodDefinition && string.Equals(m.Name, "TryResolve", StringComparison.Ordinal) && m.GetParameters().Length == 2)
                    .ToArray();
                if (genericDefs.Length == 0) return null;

                // Collect candidate getter/interface types from loaded assemblies.
                var candidateTypes = new List<Type>();
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); } catch { continue; }
                    foreach (var t in types)
                    {
                        try
                        {
                            if (!t.IsInterface) continue;
                            var n = t.Name ?? string.Empty;
                            if (n.EndsWith("Getter") || n.EndsWith("Getter`1") || n.EndsWith("RecordGetter"))
                            {
                                candidateTypes.Add(t);
                            }
                        }
                        catch { }
                    }
                }

                // Add a small set of well-known type names as a fallback if CandidateTypes is empty
                if (candidateTypes.Count == 0)
                {
                    string[] known = new[] {
                        "Mutagen.Bethesda.Fallout4.IObjectModificationGetter",
                        "Mutagen.Bethesda.Fallout4.IConstructibleObjectGetter",
                        "Mutagen.Bethesda.Fallout4.IWeaponGetter",
                        "Mutagen.Bethesda.Fallout4.IAmmunitionGetter",
                        "Mutagen.Bethesda.Fallout4.IProjectileGetter",
                        "Mutagen.Bethesda.Plugins.Records.IMajorRecordGetter"
                    };
                    foreach (var q in known)
                    {
                        try { var ty = Type.GetType(q, false); if (ty != null) candidateTypes.Add(ty); } catch { }
                    }
                }

                var unique = candidateTypes.Distinct().ToArray();
                if (unique.Length == 0) return null;

                foreach (var genDef in genericDefs)
                {
                    foreach (var candidate in unique)
                    {
                        try
                        {
                            var constructed = genDef.MakeGenericMethod(candidate);
                            var args = new object?[] { mfk, null };
                            var okObj = constructed.Invoke(linkCache, args);
                            if (okObj is bool ok && ok && args[1] != null)
                                return args[1];
                        }
                        catch (TargetInvocationException tex)
                        {
                            var inner = tex.InnerException?.Message ?? tex.Message;
                            LogErrorOnce("generic_tryresolve_targetinv", $"LinkCacheHelper: generic TryResolve<{candidate?.FullName}> invocation failed: {inner}", tex);
                        }
                        catch (Exception ex)
                        {
                            LogErrorOnce("generic_tryresolve_invoke_failed", $"LinkCacheHelper: generic TryResolve<{candidate?.FullName}> invocation error: {ex.Message}", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogErrorOnce("generic_tryresolve_scan_failed", $"LinkCacheHelper: generic TryResolve scan error: {ex.Message}", ex);
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
                            try { invoke = m.MakeGenericMethod(genArg); } catch (Exception ex) { LogErrorOnce("makegeneric_tryresolve_linklike_failed", $"LinkCacheHelper: MakeGenericMethod failed for TryResolve(linkLike): {ex.Message}", ex); continue; }
                        }
                        var p0 = invoke.GetParameters()![0]!.ParameterType;
                        var linkTypeActual = linkLike.GetType();
                        bool compatible = p0 == linkTypeActual || p0.IsAssignableFrom(linkTypeActual) || linkTypeActual.IsAssignableFrom(p0);
                        if (!compatible)
                        {
                            // Suppress expected noise when method expects string
                            if (p0 != typeof(string))
                            {
                                try
                                {
                                    LogErrorOnce("tryresolve_linklike_typemismatch", $"LinkCacheHelper: TryResolve(linkLike) type mismatch - LinkCache={linkCache.GetType().FullName}, MethodParam={p0.FullName}, LinkLikeType={linkTypeActual.FullName}");
                                }
                                catch { }
                            }

                            // If the method expects a string, skip string-based fallback for LinkLike as well.
                            if (p0 == typeof(string))
                            {
                                continue;
                            }

                            continue;
                        }

                        var args = new object?[] { linkLike, null };
                        var okObj = invoke.Invoke(linkCache, args);
                        var ok = okObj as bool? ?? (okObj is bool b && b);
                        if (ok) return args[1];
                    }
                    catch (Exception ex)
                    {
                        LogErrorOnce("tryresolve_linklike_candidate_failed", $"LinkCacheHelper: TryResolve(linkLike) candidate invocation failed: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                LogErrorOnce("tryresolve_linklike_fallback_failed", "LinkCacheHelper: unexpected error in TryResolve fallback loop", ex);
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

                        // If parameter type is not compatible, attempt conversion when appropriate
                        if (!pType.IsAssignableFrom(arg.GetType()) && pType != typeof(object))
                        {
                            if (pType != typeof(string))
                            {
                                try
                                {
                                    if (System.Threading.Interlocked.Exchange(ref s_singleArgIncompatLogged, 1) == 0)
                                    {
                                        LogErrorOnce("singlearg_incompat", $"LinkCacheHelper: single-arg fallback incompatible - LinkCacheMethod={m.Name}, ParamType={pType.FullName}, ArgType={arg.GetType().FullName} (further identical messages will be suppressed)");
                                    }
                                }
                                catch { }
                            }

                            if (pType == typeof(string))
                            {
                                // Do not attempt string-based single-arg fallbacks for FormKey/FormLink inputs.
                                continue;
                            }

                            // not compatible -> continue to next candidate
                            continue;
                        }

                        // Parameter is compatible, attempt invocation
                        try
                        {
                            var r = m.Invoke(linkCache, new object?[] { arg });
                            if (r != null && !(r is bool)) return r;
                        }
                        catch (TargetInvocationException tex)
                        {
                            var inner = tex.InnerException;
                            var innerTypeName = inner?.GetType().FullName ?? string.Empty;
                            if (innerTypeName.Contains("Mutagen.Bethesda.Plugins.Exceptions.MissingRecordException"))
                            {
                                LogErrorOnce("missing_record_resolve", $"LinkCacheHelper: single-arg invocation referenced record not found: {inner?.Message ?? tex.Message}");
                            }
                            else
                            {
                                LogErrorOnce("singlearg_targetinvocation", $"LinkCacheHelper: single-arg invocation TargetInvocationException: {inner?.Message ?? tex.Message}", tex);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"LinkCacheHelper: fallback single-arg method invocation failed: {ex}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogErrorOnce("singlearg_candidate_processing_failed", $"LinkCacheHelper: single-arg candidate processing failed: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                LogErrorOnce("singlearg_discovery_failed", "LinkCacheHelper: unexpected error in fallback single-arg discovery", ex);
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

        // Attempt to convert a FormKey/FormLink/record into the identifier string expected by LinkCache string-based APIs.
        // Returns true and sets identifier on success.
        private static bool TryConvertFormKeyToIdentifier(object? maybeRecordOrFormKey, out string identifier)
        {
            identifier = string.Empty;
            if (maybeRecordOrFormKey == null) return false;
            try
            {
                // Prefer the centralized helper which can handle records or formkey-like objects
                if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(maybeRecordOrFormKey, out var plugin, out var id))
                {
                    if (!string.IsNullOrEmpty(plugin) && id != 0u)
                    {
                        identifier = $"{plugin}:{id:X8}";
                        return true;
                    }
                }

                // Try extracting a FormKey then parsing its ToString()
                if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetFormKey(maybeRecordOrFormKey, out var fk))
                {
                    if (fk != null)
                    {
                        var fkStr = fk.ToString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(fkStr))
                        {
                            identifier = fkStr;
                            return true;
                        }
                    }
                }

                // As a last resort, use ToString() of the provided object
                var s = maybeRecordOrFormKey.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(s))
                {
                    identifier = s;
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LinkCacheHelper: TryConvertFormKeyToIdentifier failed: {ex}");
            }

            // If we reach here, create a diagnostic dump to help debugging
            try
            {
                WriteConversionDiagnosticDump(maybeRecordOrFormKey, "conversion_failed");
            }
            catch { }

            return false;
        }

        private static void WriteConversionDiagnosticDump(object? obj, string reason)
        {
            try
            {
                if (obj == null) return;
                var repoRoot = MunitionAutoPatcher.Utilities.RepoUtils.FindRepoRoot();
                var dir = Path.Combine(repoRoot ?? Environment.CurrentDirectory ?? ".", "artifacts", "linkcache_conversion_dumps");
                Directory.CreateDirectory(dir);
                var now = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
                var hash = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
                var fname = Path.Combine(dir, $"dump_{now}_{hash}.json");

                var info = new Dictionary<string, object?>();
                try { info["TypeName"] = obj.GetType().FullName; } catch { info["TypeName"] = null; }
                try { info["ToString"] = obj.ToString(); } catch { info["ToString"] = null; }
                try
                {
                    if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetFormKey(obj, out var fk) && fk != null)
                        info["FormKeyToString"] = fk.ToString();
                }
                catch { }

                try
                {
                    if (MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(obj, out var plugin, out var id))
                    {
                        info["Plugin"] = plugin;
                        info["FormId"] = id;
                    }
                }
                catch { }

                try
                {
                    var props = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                  .Select(p => p.Name).ToArray();
                    info["PublicProperties"] = props;
                }
                catch { }

                info["Reason"] = reason;
                info["TimestampUtc"] = DateTime.UtcNow.ToString("o");

                var json = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(fname, json);
                System.Diagnostics.Debug.WriteLine($"LinkCacheHelper: wrote conversion diagnostic dump: {fname}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LinkCacheHelper: failed to write conversion diagnostic dump: {ex}");
            }
        }

        private static bool IsMissingRecordException(Exception? ex)
        {
            if (ex == null) return false;
            try
            {
                var tn = ex.GetType().FullName ?? string.Empty;
                return tn.Contains("Mutagen.Bethesda.Plugins.Exceptions.MissingRecordException");
            }
            catch { return false; }
        }

        private static void LogErrorOnce(string key, string message, Exception? ex = null)
        {
            try
            {
                var newCount = s_errorCounts.AddOrUpdate(key, 1, (_, old) => old + 1);
                if (newCount <= s_errorSuppressThreshold)
                {
                    System.Diagnostics.Debug.WriteLine($"{message}: {ex}");
                }
                else if (newCount == s_errorSuppressThreshold + 1)
                {
                    System.Diagnostics.Debug.WriteLine($"{message} (further identical errors will be suppressed)");
                }
            }
            catch { }
        }

        // LinkCacheHelper previously had an internal Log helper; it now uses internal diagnostic helpers
        // (e.g. LogErrorOnce / Debug.WriteLine) and callers are expected to use `ILogger` when available.
    }
}

using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Fallout4; // For concrete getter interfaces like IObjectModificationGetter, IConstructibleObjectGetter

namespace MunitionAutoPatcher.Services.Implementations
{
    /// <summary>
    /// Version-adaptive link resolver that prefers typed resolution when available and falls back to reflection.
    /// Holds a per-scope cache to avoid repeated reflection calls during a single extraction pass.
    /// </summary>
    public sealed class LinkResolver : ILinkResolver
    {
        private readonly object _linkCache;
        private readonly ILinkCache? _typedLinkCache;
        private readonly ConcurrentDictionary<string, object?> _cache = new(StringComparer.Ordinal);

        private readonly ILogger<LinkResolver>? _logger;

        public LinkResolver(object linkCache, ILogger<LinkResolver>? logger = null)
        {
            _linkCache = linkCache ?? throw new ArgumentNullException(nameof(linkCache));
            _typedLinkCache = linkCache as ILinkCache;
            _logger = logger;
        }

        public LinkResolver(ILinkCache linkCache, ILogger<LinkResolver>? logger = null)
            : this((object)linkCache, logger)
        {
        }

        public ILinkCache? LinkCache => _typedLinkCache;

        public ILinkCache? TypedLinkCache => _typedLinkCache;

        public object RawLinkCache => _linkCache;

        public bool TryResolve(object linkLike, out object? result)
        {
            result = null;
            if (linkLike == null) return false;
            var key = BuildKey(linkLike);
            if (_cache.TryGetValue(key, out var cached))
            {
                result = cached;
                return result != null;
            }

            try
            {
                // Log attempt for diagnostics
                _logger?.LogDebug("LinkResolver.TryResolve: attempting resolve for type={Type}", linkLike.GetType().FullName);
            }
            catch { }

            // Typed fast-paths when we have a concrete ILinkCache
            try
            {
                if (_typedLinkCache != null)
                {
                    if (linkLike is Mutagen.Bethesda.Plugins.FormKey mfk)
                    {
                        if (TryTypedResolve(mfk, out var major) && major != null)
                        { _cache[key] = major; result = major; return true; }
                    }
                    else if (linkLike is FormKey ourFk)
                    {
                        var mfk2 = TryToMutagenFormKey(ourFk);
                        if (mfk2 != null && TryTypedResolve(mfk2.Value, out var major2) && major2 != null)
                        { _cache[key] = major2; result = major2; return true; }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "LinkResolver.TryResolve: typed fast-path failed");
            }

            var r = Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(linkLike, _linkCache);
            _cache[key] = r; // cache nulls too to avoid repeated attempts
            result = r;
            return r != null;
        }

        public bool TryResolve<TGetter>(object linkLike, out TGetter? result) where TGetter : class?
        {
            result = null;
            if (!TryResolve(linkLike, out var obj)) return false;
            result = obj as TGetter;
            return result != null;
        }

        public object? ResolveByKey(FormKey key)
        {
            var cacheKey = $"KEY:{key.PluginName}:{key.FormId:X8}";
            if (_cache.TryGetValue(cacheKey, out var cached))
                return cached;

            object? result = null;
            try
            {
                var mfk = TryToMutagenFormKey(key);
                if (mfk != null)
                {
                    _logger?.LogDebug("ResolveByKey: attempting Mutagen FormKey resolution {Mod}:{Id:X8}", mfk.Value.ModKey.FileName, mfk.Value.ID);

                    // Typed fast-path
                    if (_typedLinkCache != null && TryTypedResolve(mfk.Value, out var major) && major != null)
                    { result = major; _logger?.LogDebug("ResolveByKey: typed path FOUND"); }
                    else
                    {
                        result = Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(mfk.Value, _linkCache);
                        _logger?.LogDebug("ResolveByKey: reflection path result was {Result}", result != null ? "FOUND" : "MISS");
                    }
                }
                else
                {
                    // Last resort: try resolving the original internal form key via helper (some adapters accept it)
                    _logger?.LogDebug("ResolveByKey: falling back to resolving custom FormKey {Plugin}:{Id:X8}", key.PluginName, key.FormId);
                    result = Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(key, _linkCache);
                    _logger?.LogDebug("ResolveByKey: fallback result was {Result}", result != null ? "FOUND" : "MISS");
                }
            }
            catch (Exception ex)
            {
                try
                {
                    result = Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(key, _linkCache);
                }
                catch { _ = ex; result = null; }
            }

            _cache[cacheKey] = result;
            return result;
        }

        public object? ResolveByKey(object key)
        {
            try
            {
                if (key == null) return null;

                // If it's our internal FormKey type, delegate to the existing method
                if (key is FormKey fk)
                {
                    return ResolveByKey(fk);
                }

                // If it's a Mutagen FormKey, try resolving directly
                if (key is Mutagen.Bethesda.Plugins.FormKey mfk)
                {
                    try
                    {
                        var r = Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(mfk, _linkCache);
                        return r;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "ResolveByKey: failed resolving Mutagen FormKey");
                        return null;
                    }
                }

                // If the object exposes a FormKey property, try to extract and resolve it
                var fkProp = key.GetType().GetProperty("FormKey");
                if (fkProp != null)
                {
                    var rawFk = fkProp.GetValue(key);
                    if (rawFk is Mutagen.Bethesda.Plugins.FormKey mfk2)
                    {
                        try { return Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(mfk2, _linkCache); }
                        catch (Exception ex) { _logger?.LogDebug(ex, "ResolveByKey: failed resolving FormKey property (Mutagen)"); return null; }
                    }
                    if (rawFk is FormKey customFk)
                    {
                        return ResolveByKey(customFk);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "LinkResolver.ResolveByKey failed for key type: {Type}", key?.GetType().FullName);
                return null;
            }
        }

        private Mutagen.Bethesda.Plugins.FormKey? TryToMutagenFormKey(FormKey fk)
        {
            try
            {
                if (fk == null || string.IsNullOrWhiteSpace(fk.PluginName) || fk.FormId == 0) return null;
                var fileName = System.IO.Path.GetFileName(fk.PluginName) ?? fk.PluginName;
                var modType = Mutagen.Bethesda.Plugins.ModType.Plugin;
                if (fileName.EndsWith(".esm", StringComparison.OrdinalIgnoreCase)) modType = Mutagen.Bethesda.Plugins.ModType.Master;
                else if (fileName.EndsWith(".esl", StringComparison.OrdinalIgnoreCase)) modType = Mutagen.Bethesda.Plugins.ModType.Light;

                var modKey = new Mutagen.Bethesda.Plugins.ModKey(fileName, modType);
                return new Mutagen.Bethesda.Plugins.FormKey(modKey, fk.FormId);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "TryToMutagenFormKey failed for {PluginName}:{FormId:X8}", fk.PluginName, fk.FormId);
                return null;
            }
        }

        private static string BuildKey(object linkLike)
        {
            // Use object identity + type name to avoid collisions; stable enough within a single extraction pass
            return $"OBJ:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(linkLike)}:{linkLike.GetType().FullName}";
        }

        // Try common FO4 record getter types to accommodate caches requiring concrete types
        private bool TryTypedResolve(Mutagen.Bethesda.Plugins.FormKey key, out object? result)
        {
            result = null;
            var cache = _typedLinkCache;
            if (cache == null) return false;

            // Priority order: OMOD, COBJ, WEAP, AMMO, ARMO (expandable)
            Type[] types = new Type[]
            {
                typeof(IObjectModificationGetter),
                typeof(IConstructibleObjectGetter),
                typeof(IWeaponGetter),
                typeof(IAmmunitionGetter),
                typeof(IArmorGetter),
                typeof(IMajorRecordGetter)
            };

            foreach (var t in types)
            {
                try
                {
                    if (cache.TryResolve(key, t, out var major) && major != null)
                    { result = major; return true; }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "TryTypedResolve: cache.TryResolve failed for type {Type}", t.FullName);
                }
            }
            return false;
        }
    }
}

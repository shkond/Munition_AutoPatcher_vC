using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Fallout4;
using MunitionAutoPatcher.Services.Interfaces;

namespace MunitionAutoPatcher.Services.Implementations
{
    /// <summary>
    /// Simplified LinkResolver focusing on explicit typed fast-path + minimal fallback.
    /// </summary>
    public sealed class LinkResolver : ILinkResolver
    {
        private readonly ILinkCache _cache;
        private readonly ILogger<LinkResolver> _logger;
        private readonly ConcurrentDictionary<string, object?> _memo = new(StringComparer.Ordinal);

        public LinkResolver(ILinkCache cache, ILogger<LinkResolver> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ILinkCache? LinkCache => _cache;

        public bool TryResolve(object linkLike, out object? result)
        {
            result = ResolveInternal(linkLike);
            return result != null;
        }

        public bool TryResolve<TGetter>(object linkLike, out TGetter? result) where TGetter : class?
        {
            var r = ResolveInternal(linkLike);
            result = r as TGetter;
            return result != null;
        }

        public object? ResolveByKey(Models.FormKey key)
        {
            var mfk = FormKeyNormalizer.ToMutagenFormKey(key);
            return mfk.HasValue ? ResolveInternal(mfk.Value) : null;
        }

        private object? ResolveInternal(object key)
        {
            if (key == null) return null;

            string cacheKey = key switch
            {
                FormKey fk => $"FK:{fk.ModKey.FileName}:{fk.ID:X8}",
                Models.FormKey ck => $"CFK:{ck.PluginName}:{ck.FormId:X8}",
                _ => $"OBJ:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(key)}:{key.GetType().Name}"
            };

            if (_memo.TryGetValue(cacheKey, out var cached))
                return cached;

            object? resolved = null;

            try
            {
                if (key is Models.FormKey custom)
                {
                    var mfk = FormKeyNormalizer.ToMutagenFormKey(custom);
                    if (mfk.HasValue)
                        resolved = ResolveFormKeyFast(mfk.Value);
                }
                else if (key is FormKey fk)
                {
                    resolved = ResolveFormKeyFast(fk);
                }
                else
                {
                    var prop = key.GetType().GetProperty("FormKey");
                    var raw = prop?.GetValue(key);
                    if (raw is FormKey mfk2)
                        resolved = ResolveFormKeyFast(mfk2);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "LinkResolver: ResolveInternal failed for {Type}", key.GetType().FullName);
            }

            _memo[cacheKey] = resolved;
            return resolved;
        }

        private object? ResolveFormKeyFast(FormKey fk)
        {
            // Try typed paths first
            if (_cache.TryResolve<IObjectModificationGetter>(fk, out var omod) && omod != null)
            {
                _logger.LogDebug("LinkResolver: typed resolve SUCCESS IObjectModificationGetter {Mod}:{Id:X8}", fk.ModKey.FileName, fk.ID);
                return omod;
            }
            if (_cache.TryResolve<IConstructibleObjectGetter>(fk, out var cobj) && cobj != null)
            {
                _logger.LogDebug("LinkResolver: typed resolve SUCCESS IConstructibleObjectGetter {Mod}:{Id:X8}", fk.ModKey.FileName, fk.ID);
                return cobj;
            }
            if (_cache.TryResolve<IWeaponGetter>(fk, out var weap) && weap != null)
            {
                _logger.LogDebug("LinkResolver: typed resolve SUCCESS IWeaponGetter {Mod}:{Id:X8}", fk.ModKey.FileName, fk.ID);
                return weap;
            }
            if (_cache.TryResolve<IAmmunitionGetter>(fk, out var ammo) && ammo != null)
            {
                _logger.LogDebug("LinkResolver: typed resolve SUCCESS IAmmunitionGetter {Mod}:{Id:X8}", fk.ModKey.FileName, fk.ID);
                return ammo;
            }

            // Generic fallback - use typed overload to avoid obsolete warning
            if (_cache.TryResolve<IMajorRecordGetter>(fk, out var any) && any != null)
            {
                _logger.LogDebug("LinkResolver: generic resolve SUCCESS {Mod}:{Id:X8} Type={Type}", fk.ModKey.FileName, fk.ID, any.GetType().Name);
                return any;
            }

            _logger.LogDebug("LinkResolver: resolve MISS {Mod}:{Id:X8}", fk.ModKey.FileName, fk.ID);
            return null;
        }
    }
}

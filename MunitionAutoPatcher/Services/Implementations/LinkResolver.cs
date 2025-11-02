using System;
using System.Collections.Concurrent;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;

namespace MunitionAutoPatcher.Services.Implementations
{
    /// <summary>
    /// Version-adaptive link resolver that prefers typed resolution when available and falls back to reflection.
    /// Holds a per-scope cache to avoid repeated reflection calls during a single extraction pass.
    /// </summary>
    public sealed class LinkResolver : ILinkResolver
    {
        private readonly object _linkCache;
        private readonly ConcurrentDictionary<string, object?> _cache = new(StringComparer.Ordinal);

        public LinkResolver(object linkCache)
        {
            _linkCache = linkCache ?? throw new ArgumentNullException(nameof(linkCache));
        }

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
            var k = $"KEY:{key.PluginName}:{key.FormId:X8}";
            if (_cache.TryGetValue(k, out var cached))
                return cached;

            // Attempt Resolve(formKey) via helper by building a value-like formKey object if available
            // In many Mutagen setups, TryResolveViaLinkCache can accept formKey-like objects directly
            var r = Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(key, _linkCache);
            _cache[k] = r;
            return r;
        }

        private static string BuildKey(object linkLike)
        {
            // Use object identity + type name to avoid collisions; stable enough within a single extraction pass
            return $"OBJ:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(linkLike)}:{linkLike.GetType().FullName}";
        }
    }
}
